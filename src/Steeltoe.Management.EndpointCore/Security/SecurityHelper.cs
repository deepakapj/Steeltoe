﻿// Copyright 2017 the original author or authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Steeltoe.Management.Endpoint.CloudFoundry;
using System.Threading.Tasks;

namespace Steeltoe.Management.Endpoint.Security
{
    public class SecurityHelper : SecurityBase
    {
        public SecurityHelper(ICloudFoundryOptions options, ILogger logger = null)
            : base(options, logger)
        {
        }

        public async Task ReturnError(HttpContext context, SecurityResult error)
        {
            LogError(context, error);
            context.Response.Headers.Add("Content-Type", "application/json;charset=UTF-8");
            context.Response.StatusCode = (int)error.Code;
            await context.Response.WriteAsync(Serialize(error));
        }

        public void LogError(HttpContext context, SecurityResult error)
        {
            Logger.LogError("Actuator Security Error: {0} - {1}", error.Code, error.Message);
            if (Logger.IsEnabled(LogLevel.Trace))
            {
                foreach (var header in context.Request.Headers)
                {
                    Logger.LogTrace("Header: {0} - {1}", header.Key, header.Value);
                }
            }
        }
    }
}