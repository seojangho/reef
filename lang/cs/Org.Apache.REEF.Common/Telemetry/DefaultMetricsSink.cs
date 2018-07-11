﻿// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.

using System.Collections.Generic;
using Org.Apache.REEF.Tang.Annotations;
using Org.Apache.REEF.Utilities.Logging;

namespace Org.Apache.REEF.Common.Telemetry
{
    /// <summary>
    /// This default IMetricsSink is a simple implementation of IMetricsSink
    /// that logs the metrics on sink.
    /// </summary>
    internal sealed class DefaultMetricsSink : IMetricsSink
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(DefaultMetricsSink));

        [Inject]
        private DefaultMetricsSink()
        {
        }

        /// <summary>
        /// Simple sink that logs metrics.
        /// </summary>
        /// <param name="metrics">A collection of metrics.</param>
        public void Sink(IEnumerable<KeyValuePair<string, MetricRecord>> metrics)
        {
            foreach (var m in metrics)
            {
                Logger.Log(Level.Info, "Metrics - Name:{0}, Value:{1}.", m.Key, m.Value.Value);
            }
        }

        /// <summary>
        /// This is intentionally empty as we don't have any resource to release in the implementation.
        /// </summary>
        public void Dispose()
        {
        }
    }
}