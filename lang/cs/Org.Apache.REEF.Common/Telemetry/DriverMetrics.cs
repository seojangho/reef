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

using StringMetric = Org.Apache.REEF.Common.Telemetry.MetricClass<string>;

namespace Org.Apache.REEF.Common.Telemetry
{
    /// <summary>
    /// Driver metrics implementation that contains the system state.
    /// </summary>
    public sealed class DriverMetrics : IDriverMetrics
    {
        private MetricsData _metricsData;
        public static string DriverStateMetric = "DriverState";

        public DriverMetrics()
        {
            _metricsData = new MetricsData();
            var stateMetric = CreateAndRegisterMetric<StringMetric, string>(
                DriverStateMetric, "driver state.", false);
        }

        public IMetrics GetMetricsData()
        {
            return _metricsData;
        }

        public T CreateAndRegisterMetric<T, U>(
            string name, string description, bool keepUpdateHistory)
            where T : MetricBase<U>, new()
        {
            var metric = new T
            {
                Name = name,
                Description = description,
                KeepUpdateHistory = keepUpdateHistory
            };
            _metricsData.RegisterMetric(metric);
            return metric;
        }

        public bool TryGetMetric(string name, out IMetric metric)
        {
            var ret = _metricsData.TryGetMetric(name, out IMetric me);
            metric = me;
            return ret;
        }
    }
}
