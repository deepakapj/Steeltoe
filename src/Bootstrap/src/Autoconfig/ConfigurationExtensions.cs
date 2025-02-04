﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Configuration;
using Steeltoe.Management.OpenTelemetry.Exporters.Wavefront;
using System;

namespace Steeltoe.Bootstrap.Autoconfig
{
    internal static class ConfigurationExtensions
    {
        public static bool HasWavefront(this IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var options = new WavefrontExporterOptions(configuration);
            return !string.IsNullOrEmpty(options.Uri);
        }
    }
}
