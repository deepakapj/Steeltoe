﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Steeltoe.Common.Discovery;
using Steeltoe.Discovery;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Steeltoe.Extensions.Configuration.ConfigServer.Test
{
    internal class TestDiscoveryClient : IDiscoveryClient
    {
        internal bool HasShutdown = false;

        public string Description => throw new NotImplementedException();

        public IList<string> Services => throw new NotImplementedException();

        public IList<IServiceInstance> GetInstances(string serviceId)
        {
            throw new NotImplementedException();
        }

        public IServiceInstance GetLocalServiceInstance()
        {
            throw new NotImplementedException();
        }

        public Task ShutdownAsync()
        {
            HasShutdown = true;
            return Task.CompletedTask;
        }
    }
}