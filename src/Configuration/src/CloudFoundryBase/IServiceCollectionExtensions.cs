﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Steeltoe.Common;
using System.Linq;

namespace Steeltoe.Extensions.Configuration.CloudFoundry
{
    public static class IServiceCollectionExtensions
    {
        /// <summary>
        /// Removes any existing <see cref="IApplicationInstanceInfo"/> if found. Registers a <see cref="CloudFoundryApplicationOptions" />
        /// </summary>
        /// <param name="serviceCollection">Collection of configured services</param>
        public static IServiceCollection RegisterCloudFoundryApplicationInstanceInfo(this IServiceCollection serviceCollection)
        {
            var appInfo = serviceCollection.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IApplicationInstanceInfo));
            if (appInfo?.ImplementationType?.IsAssignableFrom(typeof(CloudFoundryApplicationOptions)) != true)
            {
                if (appInfo != null)
                {
                    serviceCollection.Remove(appInfo);
                }

                serviceCollection.AddSingleton(typeof(CloudFoundryApplicationOptions), serviceProvider => new CloudFoundryApplicationOptions(serviceProvider.GetRequiredService<IConfiguration>()));
                serviceCollection.AddSingleton<IApplicationInstanceInfo>(serviceProvider => serviceProvider.GetRequiredService<CloudFoundryApplicationOptions>());
            }

            return serviceCollection;
        }
    }
}
