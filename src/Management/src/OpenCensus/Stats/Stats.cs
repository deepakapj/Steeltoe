﻿// <copyright file="Stats.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace OpenCensus.Stats
{
    public class Stats
    {
        private static readonly Stats stats = new Stats();

        private readonly IStatsComponent statsComponent = new StatsComponent();

        internal Stats()
            : this(true)
        {
        }

        internal Stats(bool enabled)
        {
            if (enabled)
            {
                statsComponent = new StatsComponent();
            }
            else
            {
                statsComponent = NoopStats.NewNoopStatsComponent();
            }
        }

        public static IStatsRecorder StatsRecorder
        {
            get
            {
                return stats.statsComponent.StatsRecorder;
            }
        }

        public static IViewManager ViewManager
        {
            get
            {
                return stats.statsComponent.ViewManager;
            }
        }

        public static StatsCollectionState State
        {
            get
            {
                return stats.statsComponent.State;
            }
        }
    }
}