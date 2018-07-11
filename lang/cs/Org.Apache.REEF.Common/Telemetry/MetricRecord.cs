// Licensed to the Apache Software Foundation (ASF) under one
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

using System;
using System.Threading;
using Newtonsoft.Json;

namespace Org.Apache.REEF.Common.Telemetry
{
    [JsonObject]
    public class MetricRecord
    {
        private object _value;

        [JsonProperty]
        public object Value
        {
            get
            {
                return _value;
            }
        }
        [JsonProperty]
        public long Timestamp { get; }

        [JsonConstructor]
        public MetricRecord(object value, long timestamp)
        {
            _value = value;
            Timestamp = timestamp;
        }

        public MetricRecord(IMetric metric)
        {
            Timestamp = DateTime.Now.Ticks;
            Interlocked.Exchange(ref _value, metric.ValueUntyped);
        }

        public MetricRecord(object val)
        {
            _value = val;
            Timestamp = DateTime.Now.Ticks;
        }
    }
}
