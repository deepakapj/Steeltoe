﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading.Tasks;

namespace Steeltoe.Management.Endpoint.ThreadDump
{
    /// <summary>
    /// Thread dumper that uses the EventPipe to acquire the call stacks of all the running Threads
    /// </summary>
    public class ThreadDumperEP : IThreadDumper
    {
        private static readonly StackTraceElement _unknownStackTraceElement = new StackTraceElement()
        {
            ClassName = "[UnknownClass]",
            MethodName = "[UnknownMethod]",
            IsNativeMethod = true
        };

        private static readonly StackTraceElement _nativeStackTraceElement = new StackTraceElement()
        {
            ClassName = "[NativeClasses]",
            MethodName = "[NativeMethods]",
            IsNativeMethod = true
        };

        private readonly ILogger<ThreadDumperEP> _logger;
        private readonly IThreadDumpOptions _options;

        public ThreadDumperEP(IThreadDumpOptions options, ILogger<ThreadDumperEP> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        /// <summary>
        /// Connect using the EventPipe and obtain a dump of all the Threads and for each thread a stacktrace
        /// </summary>
        /// <returns>the list of threads with stack trace information</returns>
        public List<ThreadInfo> DumpThreads()
        {
            var results = new List<ThreadInfo>();
            try
            {
                _logger?.LogDebug("Starting thread dump");

                var client = new DiagnosticsClient(System.Diagnostics.Process.GetCurrentProcess().Id);
                var providers = new List<EventPipeProvider>()
                {
                    new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", EventLevel.Informational)
                };

                using (var session = client.StartEventPipeSession(providers))
                {
                    DumpThreads(session, results);
                }
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "Unable to dump threads");
            }
            finally
            {
                var totalMemory = GC.GetTotalMemory(true);
                _logger?.LogDebug("Total Memory {0}", totalMemory);
            }

            return results;
        }

        // Much of this code is from diagnostics/dotnet-stack
        private void DumpThreads(EventPipeSession session, List<ThreadInfo> results)
        {
            string traceFileName = null;
            try
            {
                traceFileName = CreateTraceFile(session).Result;
                if (traceFileName != null)
                {
                   using (var symbolReader = new SymbolReader(System.IO.TextWriter.Null) { SymbolPath = Environment.CurrentDirectory })
                    using (var eventLog = new TraceLog(traceFileName))
                    {
                        var stackSource = new MutableTraceEventStackSource(eventLog)
                        {
                            OnlyManagedCodeStacks = true
                        };

                        var computer = new SampleProfilerThreadTimeComputer(eventLog, symbolReader);
                        computer.GenerateThreadTimeStacks(stackSource);

                        var samplesForThread = new Dictionary<int, List<StackSourceSample>>();

                        stackSource.ForEach((sample) =>
                        {
                            var stackIndex = sample.StackIndex;
                            while (!stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false).StartsWith("Thread ("))
                            {
                                stackIndex = stackSource.GetCallerIndex(stackIndex);
                            }

                            var template = "Thread (";
                            var threadFrame = stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false);
                            var threadId = int.Parse(threadFrame.Substring(template.Length, threadFrame.Length - (template.Length + 1)));

                            if (samplesForThread.TryGetValue(threadId, out var samples))
                            {
                                samples.Add(sample);
                            }
                            else
                            {
                                samplesForThread[threadId] = new List<StackSourceSample>() { sample };
                            }
                        });

                        foreach (var (threadId, samples) in samplesForThread)
                        {
                            _logger?.LogDebug("Found {0} stacks for thread {1}", samples.Count, threadId);
                            var threadInfo = new ThreadInfo()
                            {
                                ThreadId = threadId,
                                ThreadName = "Thread-" + threadId,
                                ThreadState = TState.RUNNABLE,
                                IsInNative = false,
                                IsSuspended = false,
                                LockedMonitors = new List<MonitorInfo>(),
                                LockedSynchronizers = new List<LockInfo>()
                            };
                            threadInfo.StackTrace = GetStackTrace(threadId, samples[0], stackSource, symbolReader);
                            threadInfo.ThreadState = GetThreadState(threadInfo.StackTrace);
                            threadInfo.IsInNative = IsThreadInNative(threadInfo.StackTrace);
                            results.Add(threadInfo);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "Error processing trace file for thread dump");
                results.Clear();
            }
            finally
            {
                if (File.Exists(traceFileName))
                {
                    File.Delete(traceFileName);
                }
            }

            _logger?.LogTrace("Finished thread walk");
        }

        private bool IsThreadInNative(List<StackTraceElement> frames)
        {
            if (frames.Count > 0)
            {
                return frames[0] == _nativeStackTraceElement;
            }

            return false;
        }

        private TState GetThreadState(List<StackTraceElement> frames)
        {
            if (IsThreadInNative(frames) && frames.Count > 1 && frames[1].MethodName.Contains("Wait", StringComparison.OrdinalIgnoreCase))
            {
                return TState.WAITING;
            }

            return TState.RUNNABLE;
        }

        private List<StackTraceElement> GetStackTrace(int threadId, StackSourceSample stackSourceSample, TraceEventStackSource stackSource, SymbolReader symbolReader)
        {
            _logger?.LogDebug("Processing thread with ID: {0}", threadId);

            var result = new List<StackTraceElement>();

            var stackIndex = stackSourceSample.StackIndex;
            while (!stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), verboseName: false).StartsWith("Thread ("))
            {
                var frameIndex = stackSource.GetFrameIndex(stackIndex);
                var frameName = stackSource.GetFrameName(frameIndex, verboseName: false);
                var sourceLine = GetSourceLine(stackSource, frameIndex, symbolReader);
                var stackElement = GetStackTraceElement(frameName, sourceLine);
                if (stackElement != null)
                {
                    result.Add(stackElement);
                }

                stackIndex = stackSource.GetCallerIndex(stackIndex);
            }

            return result;
        }

        private StackTraceElement GetStackTraceElement(string frameName, SourceLocation sourceLocation)
        {
            if (string.IsNullOrEmpty(frameName))
            {
                return _unknownStackTraceElement;
            }

            if (frameName.Contains("UNMANAGED_CODE_TIME", StringComparison.OrdinalIgnoreCase) ||
                frameName.Contains("CPU_TIME", StringComparison.OrdinalIgnoreCase))
            {
                return _nativeStackTraceElement;
            }

            var result = new StackTraceElement()
            {
                MethodName = "Unknown",
                IsNativeMethod = false,
                ClassName = "Unknown"
            };

            if (ParseFrameName(frameName, out var assemblyName, out var className, out var methodName, out var parameters))
            {
                result.ClassName = assemblyName + "!" + className;
                result.MethodName = methodName + parameters;
            }

            if (sourceLocation != null)
            {
                result.LineNumber = sourceLocation.LineNumber;
                if (sourceLocation.SourceFile != null)
                {
                    result.FileName = sourceLocation.SourceFile.BuildTimeFilePath;
                }
            }

            return result;
        }

        private bool ParseFrameName(string frameName, out string assemblyName, out string className, out string methodName, out string parameters)
        {
            assemblyName = null;
            className = null;
            methodName = null;
            parameters = null;

            if (string.IsNullOrEmpty(frameName))
            {
                return false;
            }

            string remaining = null;

            if (!ParseParameters(frameName, ref remaining, ref parameters))
            {
                return false;
            }

            if (!ParseMethod(remaining, ref remaining, ref methodName))
            {
                return false;
            }

            if (!ParseClassName(remaining, ref remaining, ref className))
            {
                return false;
            }

            var extIndex = remaining.IndexOf('.');
            if (extIndex > 0)
            {
                assemblyName = remaining.Substring(0, extIndex);
            }
            else
            {
                assemblyName = remaining;
            }

            return true;
        }

        private bool ParseClassName(string input, ref string remaining, ref string className)
        {
            var classStartIndex = input.IndexOf('!');
            if (classStartIndex == -1)
            {
                className = input;
                remaining = string.Empty;
            }
            else
            {
                remaining = input.Substring(0, classStartIndex);
                className = input.Substring(classStartIndex + 1);
            }

            return true;
        }

        private bool ParseMethod(string input, ref string remaining, ref string methodName)
        {
            var methodStartIndex = input.LastIndexOf('.');
            if (methodStartIndex > 0)
            {
                remaining = input.Substring(0, methodStartIndex);
                methodName = input.Substring(methodStartIndex + 1);
                return true;
            }

            return false;
        }

        private bool ParseParameters(string input, ref string remaining, ref string parameters)
        {
            var paramStartIndex = input.IndexOf('(');
            if (paramStartIndex > 0)
            {
                remaining = input.Substring(0, paramStartIndex);
                parameters = input.Substring(paramStartIndex);
                return true;
            }

            return false;
        }

        // Much of this code is from PerfView/TraceLog.cs
        private SourceLocation GetSourceLine(TraceEventStackSource stackSource, StackSourceFrameIndex frameIndex, SymbolReader reader)
        {
            var log = stackSource.TraceLog;
            var codeAddress = (uint)frameIndex - (uint)StackSourceFrameIndex.Start;
            if (codeAddress >= log.CodeAddresses.Count)
            {
                return null;
            }

            var codeAddressIndex = (CodeAddressIndex)codeAddress;
            var moduleFile = log.CodeAddresses.ModuleFile(codeAddressIndex);
            if (moduleFile == null)
            {
                _logger?.LogTrace("GetSourceLine: Could not find moduleFile {0:x}.", log.CodeAddresses.Address(codeAddressIndex));
                return null;
            }

            var methodIndex = log.CodeAddresses.MethodIndex(codeAddressIndex);
            if (methodIndex == MethodIndex.Invalid)
            {
                _logger?.LogTrace("GetSourceLine: Could not find method for {0:x}", log.CodeAddresses.Address(codeAddressIndex));
                return null;
            }

            var methodToken = log.CodeAddresses.Methods.MethodToken(methodIndex);
            if (methodToken == 0)
            {
                _logger?.LogTrace("GetSourceLine: Could not find method for {0:x}", log.CodeAddresses.Address(codeAddressIndex));
                return null;
            }

            var ilOffset = log.CodeAddresses.ILOffset(codeAddressIndex);
            if (ilOffset < 0)
            {
                ilOffset = 0;
            }

            string pdbFileName = null;

            if (moduleFile.PdbSignature != Guid.Empty)
            {
                pdbFileName = reader.FindSymbolFilePath(moduleFile.PdbName, moduleFile.PdbSignature, moduleFile.PdbAge, moduleFile.FilePath, moduleFile.ProductVersion, true);
            }

            if (pdbFileName == null)
            {
                var simpleName = Path.GetFileNameWithoutExtension(moduleFile.FilePath);
                if (simpleName.EndsWith(".il"))
                {
                    simpleName = Path.GetFileNameWithoutExtension(simpleName);
                }

                pdbFileName = reader.FindSymbolFilePath(simpleName + ".pdb", Guid.Empty, 0);
            }

            if (pdbFileName != null)
            {
                var symbolReaderModule = reader.OpenSymbolFile(pdbFileName);
                if (symbolReaderModule != null)
                {
                    if (moduleFile.PdbSignature != Guid.Empty && symbolReaderModule.PdbGuid != moduleFile.PdbSignature)
                    {
                        _logger?.LogTrace("ERROR: the PDB we opened does not match the PDB desired.  PDB GUID = " + symbolReaderModule.PdbGuid + " DESIRED GUID = " + moduleFile.PdbSignature);
                        return null;
                    }

                    symbolReaderModule.ExePath = moduleFile.FilePath;
                    return symbolReaderModule.SourceLocationForManagedCode((uint)methodToken, ilOffset);
                }
            }

            return null;
        }

        private async Task<string> CreateTraceFile(EventPipeSession session)
        {
            var tempNetTraceFilename = Path.GetRandomFileName() + ".nettrace";
            try
            {
                using (var fs = File.OpenWrite(tempNetTraceFilename))
                {
                    var copyTask = session.EventStream.CopyToAsync(fs);
                    await Task.Delay(_options.Duration);
                    session.Stop();

                    // check if rundown is taking more than 5 seconds and log
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                    var completedTask = await Task.WhenAny(copyTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        _logger?.LogInformation("Sufficiently large applications can cause this command to take non-trivial amounts of time");
                    }

                    await copyTask;
                }

                // using the generated trace file, symbolocate and compute stacks.
                return TraceLog.CreateFromEventPipeDataFile(tempNetTraceFilename);
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "Error creating trace file for thread dump");
                return null;
            }
            finally
            {
                if (File.Exists(tempNetTraceFilename))
                {
                    File.Delete(tempNetTraceFilename);
                }
            }
        }
    }
}
