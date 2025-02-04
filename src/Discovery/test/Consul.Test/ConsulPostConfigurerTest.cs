﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Steeltoe.Discovery.Consul.Test
{
    public class ConsulPostConfigurerTest
    {
        [Fact]
        public void ValidateOptionsComplainsAboutDefaultWhenWontWork()
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");

            var exception = Assert.Throws<InvalidOperationException>(() => ConsulPostConfigurer.ValidateConsulOptions(new ConsulOptions()));
            Assert.Contains("localhost", exception.Message);
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", null);
        }
    }
}
