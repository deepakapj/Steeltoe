﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using Steeltoe.Management.Endpoint.Test;
using Steeltoe.Management.Endpoint.Test.Infrastructure;
using Steeltoe.Management.OpenTelemetry;
using Steeltoe.Management.OpenTelemetry.Metrics;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Steeltoe.Management.Endpoint.Metrics.Test
{
    public class MetricsEndpointTest : BaseTest
    {
        private readonly ITestOutputHelper _output;

        public MetricsEndpointTest(ITestOutputHelper output)
        {
            _output = output;
            OpenTelemetryMetrics.InstrumentationName = Guid.NewGuid().ToString();
        }

        [Fact]
        public void Constructor_ThrowsIfNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new MetricsEndpoint(null, null, null));
            Assert.Throws<ArgumentNullException>(() => new MetricsEndpoint(new MetricsEndpointOptions(), null, null));
        }

        [Fact]
        public void Invoke_WithNullMetricsRequest_ReturnsExpected()
        {
            using (var tc = new TestContext(_output))
            {
                tc.AdditionalServices = (services, configuration) =>
                {
                    services.AddMetricsActuatorServices(configuration);
                };

                var meterFactory = tc.GetService<MeterProvider>();
                var ep = tc.GetService<IMetricsEndpoint>();
                var requests = OpenTelemetryMetrics.Meter.CreateCounter<long>("http.server.requests");
                requests.Add(1);
                var memory = OpenTelemetryMetrics.Meter.CreateCounter<double>("gc.memory.used");
                memory.Add(25);

                var result = ep.Invoke(null);
                Assert.NotNull(result);
                Assert.IsType<MetricsListNamesResponse>(result);
                var resp = result as MetricsListNamesResponse;
                Assert.NotEmpty(resp.Names);
                Assert.Contains("http.server.requests", resp.Names);
                Assert.Contains("gc.memory.used", resp.Names);

                Assert.Equal(2, resp.Names.Count);
            }

            using (var tc = new TestContext(_output))
            {
                tc.AdditionalServices = (services, configuration) =>
                {
                    services.AddMetricsActuatorServices(configuration);
                };
                var meterFactory = tc.GetService<MeterProvider>();
                var ep = tc.GetService<IMetricsEndpoint>();
                var result = ep.Invoke(null);
                Assert.NotNull(result);

                Assert.IsType<MetricsListNamesResponse>(result);
                var resp = result as MetricsListNamesResponse;
                Assert.Empty(resp.Names);
            }
        }

        [Fact]
        public void Invoke_WithMetricsRequest_ReturnsExpected()
        {
            using (var tc = new TestContext(_output))
            {
                tc.AdditionalServices = (services, configuration) =>
                {
                    services.AddMetricsActuatorServices(configuration);
                };
                var meterFactory = tc.GetService<MeterProvider>();
                var ep = tc.GetService<IMetricsEndpoint>();

                var testMeasure = OpenTelemetryMetrics.Meter.CreateCounter<double>("test.test5");
                long allKeyssum = 0;
                var labelsKvps = new Dictionary<string, object>() { { "a", "v1" }, { "b", "v1" }, { "c", "v1" } };

                for (var i = 0; i < 10; i++)
                {
                    allKeyssum += i;
                    testMeasure.Add(i, labelsKvps.AsReadonlySpan());
                }

                var tags = labelsKvps.Select(x => new KeyValuePair<string, string>(x.Key, x.Value.ToString())).ToList();
                var req = new MetricsRequest("test.test5", tags);
                var resp = ep.Invoke(req) as MetricsResponse;
                Assert.NotNull(resp);

                Assert.Equal("test.test5", resp.Name);

                Assert.NotNull(resp.Measurements);
                Assert.Single(resp.Measurements);

                var sample = resp.Measurements.SingleOrDefault(x => x.Statistic == MetricStatistic.TOTAL);
                Assert.NotNull(sample);
                Assert.Equal((double)allKeyssum, sample.Value);

                Assert.NotNull(resp.AvailableTags);
                Assert.Equal(3, resp.AvailableTags.Count);

                req = new MetricsRequest("foo.bar", tags);
                resp = ep.Invoke(req) as MetricsResponse;
                Assert.Null(resp);
            }
        }

        // [Fact]
        // public void GetStatistic_ReturnsExpected()
        // {
        //    var opts = new MetricsEndpointOptions();
        //    var exporter = new SteeltoeExporter();
        //    var ep = new MetricsEndpoint(opts, exporter);

        // var m1 = MeasureDouble.Create("test.totalTime", "test", MeasureUnit.Seconds);
        //    var result = ep.GetStatistic(Sum.Create(), m1);
        //    Assert.Equal(MetricStatistic.TOTALTIME, result);

        // var m2 = MeasureDouble.Create("test.value", "test", MeasureUnit.Seconds);
        //    result = ep.GetStatistic(LastValue.Create(), m2);
        //    Assert.Equal(MetricStatistic.VALUE, result);

        // var m3 = MeasureDouble.Create("test.count", "test", MeasureUnit.Seconds);
        //    result = ep.GetStatistic(Count.Create(), m3);
        //    Assert.Equal(MetricStatistic.COUNT, result);

        // var m4 = MeasureDouble.Create("test.sum", "test", MeasureUnit.Bytes);
        //    result = ep.GetStatistic(Sum.Create(), m4);
        //    Assert.Equal(MetricStatistic.TOTAL, result);

        // var m5 = MeasureDouble.Create("foobar", "test", MeasureUnit.Seconds);
        //    result = ep.GetStatistic(Distribution.Create(BucketBoundaries.Create(new List<double>() { 0.0, 1.0, 5.0, 10.0, 100.0 })), m5);
        //    Assert.Equal(MetricStatistic.TOTALTIME, result);

        // var m6 = MeasureDouble.Create("foobar", "test", MeasureUnit.Bytes);
        //    result = ep.GetStatistic(Distribution.Create(BucketBoundaries.Create(new List<double>() { 0.0, 1.0, 5.0, 10.0, 100.0 })), m6);
        //    Assert.Equal(MetricStatistic.TOTAL, result);
        // }
        [Fact]
        public void GetMetricSamples_ReturnsExpectedCounter()
        {
            using (var tc = new TestContext(_output))
            {
                tc.AdditionalServices = (services, configuration) =>
                {
                    services.AddMetricsActuatorServices(configuration);
                };
                var meterFactory = tc.GetService<MeterProvider>();
                var ep = tc.GetService<MetricsEndpoint>();

                var counter = OpenTelemetryMetrics.Meter.CreateCounter<double>("test.test7");
                counter.Add(100);

                ep.GetMetricsCollection(out var measurements, out _);
                Assert.NotNull(measurements);
                Assert.Single(measurements.Values);
                var sample = measurements.Values.FirstOrDefault()[0];
                Assert.Equal(100, sample.Value);
                Assert.Equal(MetricStatistic.TOTAL, sample.Statistic);
            }
        }

        // [Fact]
        // public void GetMetricSamples_ReturnsExpectedMeasure()
        // {
        //    using (var tc = new TestContext(_output))
        //    {
        //        tc.AdditionalServices = (services, configuration) =>
        //        {
        //            services.AddMetricsActuatorServices(configuration);
        //        };

        // var ep = tc.GetService<MetricsEndpoint>();

        // var measure = _meter.CreateObservableGauge<double>("test.test3", () => 100);

        // ep.GetMetricsCollection(out var measurements, out _);
        //        Assert.Single(measurements.Values);
        //        var sample = measurements.Values.FirstOrDefault()[0];
        //        Assert.Equal(100, sample.Value);
        //        Assert.Equal(MetricStatistic.VALUE, sample.Statistic);
        //    }
        // }

        // TODO: Support other aggregations (Not supported by OTEL yet)
        /*
        SetupTestView(stats, Sum.Create(), null, "test.test2");
        viewData = stats.ViewManager.GetView(ViewName.Create("test.test2"));
        aggData = SumDataLong.Create(100);

        Assert.NotNull(viewData);
        result = ep.GetMetricSamples(aggData, viewData);
        Assert.NotNull(result);
        Assert.Single(result);
        sample = result[0];
        Assert.Equal(100, sample.Value);
        Assert.Equal(MetricStatistic.TOTALTIME, sample.Statistic);

        SetupTestView(stats, Mean.Create(), null, "test.test4");
        viewData = stats.ViewManager.GetView(ViewName.Create("test.test4"));
        aggData = MeanData.Create(100, 50, 1, 500);

        Assert.NotNull(viewData);
        result = ep.GetMetricSamples(aggData, viewData);
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        sample = result[0];
        Assert.Equal(50, sample.Value);
        Assert.Equal(MetricStatistic.COUNT, sample.Statistic);
        sample = result[1];
        Assert.Equal(100 * 50, sample.Value);
        Assert.Equal(MetricStatistic.TOTALTIME, sample.Statistic);

        SetupTestView(stats, Distribution.Create(BucketBoundaries.Create(new List<double>() { 0.0, 10.0, 20.0 })), null, "test.test5");
        viewData = stats.ViewManager.GetView(ViewName.Create("test.test5"));
        aggData = DistributionData.Create(100, 50, 5, 200, 5, new List<long>() { 10, 20, 20 });

        Assert.NotNull(viewData);
        result = ep.GetMetricSamples(aggData, viewData);
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        sample = result[0];
        Assert.Equal(50, sample.Value);
        Assert.Equal(MetricStatistic.COUNT, sample.Statistic);

        sample = result[1];
        Assert.Equal(200, sample.Value);
        Assert.Equal(MetricStatistic.MAX, sample.Statistic);

        sample = result[2];
        Assert.Equal(100 * 50, sample.Value);
        Assert.Equal(MetricStatistic.TOTALTIME, sample.Statistic);
     }
     */
        [Fact]
        public void GetAvailableTags_ReturnsExpected()
        {
            using (var tc = new TestContext(_output))
            {
                tc.AdditionalServices = (services, configuration) =>
                {
                    services.AddMetricsActuatorServices(configuration);
                };
                var meterFactory = tc.GetService<MeterProvider>();
                var ep = tc.GetService<MetricsEndpoint>();
                var counter = OpenTelemetryMetrics.Meter.CreateCounter<double>("test.test2");

                var v1Tags = new Dictionary<string, object>()
                {
                    { "a", "v1" },
                    { "b", "v1" },
                    { "c", "v1" }
                };

                var v2Tags = new Dictionary<string, object>()
                {
                    { "a", "v2" },
                    { "b", "v2" },
                    { "c", "v2" }
                };

                counter.Add(1, v1Tags.AsReadonlySpan());
                counter.Add(1, v2Tags.AsReadonlySpan());

                ep.GetMetricsCollection(out _, out var tagDictionary);

                Assert.NotNull(tagDictionary);
                Assert.Single(tagDictionary.Values);

                var tags = tagDictionary["test.test2"];

                Assert.Equal(3, tags.Count);

                var tag = tags[0];
                Assert.NotNull(tag);
                Assert.Contains("v1", tag.Values);
                Assert.Contains("v2", tag.Values);

                tag = tags[1];
                Assert.Equal("b", tag.Tag);
                Assert.Contains("v1", tag.Values);
                Assert.Contains("v2", tag.Values);

                tag = tags[2];
                Assert.Equal("c", tag.Tag);
                Assert.Contains("v1", tag.Values);
                Assert.Contains("v2", tag.Values);

                var counter2 = OpenTelemetryMetrics.Meter.CreateCounter<double>("test.test3");

                counter2.Add(1);

                ep.GetMetricsCollection(out _, out tagDictionary);

                Assert.NotNull(tagDictionary);
                Assert.Single(tagDictionary.Values);

                tags = tagDictionary["test.test3"];
                Assert.Empty(tags);
            }
        }

        [Fact]
        public void GetMetricMeasurements_ReturnsExpected()
        {
            using (var tc = new TestContext(_output))
            {
                tc.AdditionalServices = (services, configuration) =>
                {
                    services.AddMetricsActuatorServices(configuration);
                };
                var meterFactory = tc.GetService<MeterProvider>();
                var ep = tc.GetService<MetricsEndpoint>();

                var testMeasure = OpenTelemetryMetrics.Meter.CreateHistogram<double>("test.test1");
                var context1 = new Dictionary<string, object>()
                {
                    { "a", "v1" },
                    { "b", "v1" },
                    { "c", "v1" }
                };
                var context2 = new Dictionary<string, object>()
                {
                    { "a", "v1" },
                };
                var context3 = new Dictionary<string, object>()
                {
                    { "b", "v1" },
                };
                var context4 = new Dictionary<string, object>()
                {
                    { "c", "v1" },
                };

                long allKeyssum = 0;
                for (var i = 0; i < 10; i++)
                {
                    allKeyssum += i;
                    testMeasure.Record(i, context1.AsReadonlySpan());
                }

                long asum = 0;
                for (var i = 0; i < 10; i++)
                {
                    asum += i;
                    testMeasure.Record(i, context2.AsReadonlySpan());
                }

                long bsum = 0;
                for (var i = 0; i < 10; i++)
                {
                    bsum += i;
                    testMeasure.Record(i, context3.AsReadonlySpan());
                }

                long csum = 0;
                for (var i = 0; i < 10; i++)
                {
                    csum += i;
                    testMeasure.Record(i, context4.AsReadonlySpan());
                }

                var alltags = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("a", "v1"),
                    new KeyValuePair<string, string>("b", "v1"),
                    new KeyValuePair<string, string>("c", "v1")
                };

                ep.GetMetricsCollection(out var measurements, out var tags);
                Assert.NotNull(measurements);
                Assert.Single(measurements);

                var measurement = measurements["test.test1"];
                Assert.Equal(4, measurement.Count);

                var sample = measurement[0];
                Assert.Equal((double)allKeyssum, sample.Value);
                Assert.Equal(MetricStatistic.TOTAL, sample.Statistic);

                var atags = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("a", "v1"),
                };

                var result = ep.GetMetricSamplesByTags(measurements, "test.test1", atags);
                Assert.NotNull(result);
                Assert.Single(result);

                sample = result[0];
                Assert.Equal(allKeyssum + asum, sample.Value);
                Assert.Equal(MetricStatistic.TOTAL, sample.Statistic);

                var btags = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("b", "v1"),
                };

                result = ep.GetMetricSamplesByTags(measurements, "test.test1", btags);

                Assert.NotNull(result);
                Assert.Single(result);

                sample = result[0];

                Assert.Equal(allKeyssum + bsum, sample.Value);
                Assert.Equal(MetricStatistic.TOTAL, sample.Statistic);

                var ctags = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("c", "v1"),
                };

                result = ep.GetMetricSamplesByTags(measurements, "test.test1", ctags);
                Assert.NotNull(result);
                Assert.Single(result);

                sample = result[0];
                Assert.Equal(allKeyssum + csum, sample.Value);
                Assert.Equal(MetricStatistic.TOTAL, sample.Statistic);

                var abtags = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("a", "v1"),
                    new KeyValuePair<string, string>("b", "v1"),
                };

                result = ep.GetMetricSamplesByTags(measurements, "test.test1", abtags);

                Assert.NotNull(result);
                Assert.Single(result);

                sample = result[0];
                Assert.Equal(allKeyssum, sample.Value);
                Assert.Equal(MetricStatistic.TOTAL, sample.Statistic);

                var actags = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("a", "v1"),
                    new KeyValuePair<string, string>("c", "v1"),
                };
                result = ep.GetMetricSamplesByTags(measurements, "test.test1", actags);

                Assert.NotNull(result);
                Assert.Single(result);

                sample = result[0];

                Assert.Equal(allKeyssum, sample.Value);
                Assert.Equal(MetricStatistic.TOTAL, sample.Statistic);

                var bctags = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("b", "v1"),
                    new KeyValuePair<string, string>("c", "v1"),
                };
                result = ep.GetMetricSamplesByTags(measurements, "test.test1", bctags);

                Assert.NotNull(result);
                Assert.Single(result);

                sample = result[0];

                Assert.Equal(allKeyssum, sample.Value);
                Assert.Equal(MetricStatistic.TOTAL, sample.Statistic);
            }
        }

        [Fact]
        public void GetMetric_ReturnsExpected()
        {
            using (var tc = new TestContext(_output))
            {
                tc.AdditionalServices = (services, configuration) =>
                {
                    services.AddMetricsActuatorServices(configuration);
                };
                var meterFactory = tc.GetService<MeterProvider>();
                var ep = tc.GetService<IMetricsEndpoint>();

                var testMeasure = OpenTelemetryMetrics.Meter.CreateCounter<double>("test.total");
                var labels = new Dictionary<string, object>() { { "a", "v1" }, { "b", "v1" }, { "c", "v1" } };

                double allKeyssum = 0;
                for (double i = 0; i < 10; i++)
                {
                    allKeyssum += i;
                    testMeasure.Add(i, labels.AsReadonlySpan());
                }

                var req = new MetricsRequest("test.total", labels.Select(x => new KeyValuePair<string, string>(x.Key, x.Value.ToString())).ToList());

                var resp = ep.Invoke(req) as MetricsResponse;

                Assert.NotNull(resp);

                Assert.Equal("test.total", resp.Name);

                Assert.NotNull(resp.Measurements);
                Assert.Single(resp.Measurements);
                var sample = resp.Measurements[0];
                Assert.Equal(MetricStatistic.TOTAL, sample.Statistic);
                Assert.Equal(allKeyssum, sample.Value);

                Assert.NotNull(resp.AvailableTags);
                Assert.Equal(3, resp.AvailableTags.Count);
            }
        }
    }
}
