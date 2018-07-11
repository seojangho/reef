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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Org.Apache.REEF.Tang.Annotations;
using Newtonsoft.Json;

namespace Org.Apache.REEF.Common.Telemetry
{
    /// <summary>
    /// This class maintains a collection of the data for all the metrics for metrics service. 
    /// When new metric data is received, the data in the collection will be updated.
    /// After the data is processed, the changes since last process will be reset.
    /// </summary>
    public sealed class MetricsData : IMetrics
    {
        JsonSerializerSettings settings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All
        };

        /// <summary>
        /// Registration of metrics
        /// </summary>
        private readonly ConcurrentDictionary<string, MetricTracker> _metricsMap =
            new ConcurrentDictionary<string, MetricTracker>();

        [Inject]
        internal MetricsData()
        {
        }

        /// <summary>
        /// Deserialization.
        /// </summary>
        /// <param name="serializedMetricsString">string to Deserialize.</param>
        [JsonConstructor]
        internal MetricsData(string serializedMetricsString)
        {
            var metrics = JsonConvert.DeserializeObject<IList<MetricTracker>>(serializedMetricsString, settings);

            foreach (var m in metrics)
            {
                _metricsMap.TryAdd(m.GetMetric().Name, m);
            }
        }

        public void RegisterMetric<T>(IMetric<T> metric)
        {
            if (!_metricsMap.TryAdd(metric.Name, new MetricTracker<T>(metric)))
            {
                throw new ArgumentException("The metric [{0}] already exists.", metric.Name);
            }
        }

        public bool TryGetMetric(string name, out IMetric me)
        {
            if (!_metricsMap.TryGetValue(name, out MetricTracker tracker))
            {
                me = null;
                return false;
            }
            me = tracker.GetMetric();
            return true;
        }

        public IEnumerable<MetricTracker> GetMetricTrackers()
        {
            return _metricsMap.Values;
        }

        /// <summary>
        /// Flushes changes since last sink for each metric. 
        /// Called when Driver is sinking metrics.
        /// </summary>
        /// <returns>Key value pairs of metric name and record that was flushed.</returns>
        public IEnumerable<KeyValuePair<string, MetricRecord>> FlushMetricRecords()
        {
            // for each metric, flush the records and create key value pairs
            return _metricsMap.SelectMany(kv => kv.Value.FlushChangesSinceLastSink().Select(
                r => new KeyValuePair<string, MetricRecord>(kv.Key, r)));
        }

        /// <summary>
        /// Updates metrics given another <see cref="MetricsData"/> object.
        /// For every metric in the new set, if it is registered then update the value,
        /// if it is not then add it to the registration.
        /// </summary>
        /// <param name="metrics">New metric values to be updated.</param>
        internal void Update(IMetrics metrics)
        {
            foreach (var tracker in metrics.GetMetricTrackers())
            {
                _metricsMap.AddOrUpdate(tracker.GetMetric().Name, tracker, (k, v) => v.UpdateMetric(tracker));
            }
        }

        /// <summary>
        /// Flushes that trackers contained in the queue.
        /// Called when Evaluator is sending metrics information to Driver.
        /// </summary>
        /// <returns>Queue of trackers containing metric records.</returns>
        internal IEnumerable<MetricTracker> FlushMetricTrackers()
        {
            return new ConcurrentQueue<MetricTracker>(_metricsMap.Values.Select(metric => metric.Copy()));
        }

        /// <summary>
        /// Sums up the total changes to metrics to see if it has reached the sink threshold.
        /// </summary>
        /// <returns>Returns whether the sink threshold has been met.</returns>
        internal bool TriggerSink(int metricSinkThreshold)
        {
            return _metricsMap.Values.Sum(e => e.ChangesSinceLastSink) > metricSinkThreshold;
        }

        public string Serialize()
        {
            return Serialize(_metricsMap.Values);
        }

        internal string Serialize(IEnumerable<MetricTracker> trackers)
        {
            return JsonConvert.SerializeObject(trackers.Where(me => me.ChangesSinceLastSink > 0).ToList(), settings);
        }

        internal string SerializeAndReset()
        {
            return Serialize(FlushMetricTrackers());
        }
    }
}
