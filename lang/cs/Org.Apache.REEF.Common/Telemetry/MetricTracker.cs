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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using Org.Apache.REEF.Utilities.Logging;

namespace Org.Apache.REEF.Common.Telemetry
{
    [JsonObject]
    public abstract class MetricTracker
    {
        [JsonProperty]
        internal bool KeepUpdateHistory;

        /// <summary>
        /// if KeepUpdateHistory is true, keeps a history of updates.
        /// </summary>
        [JsonProperty]
        protected ConcurrentQueue<MetricRecord> Records;

        /// <summary>
        /// Number of times metric has been updated since last processed.
        /// </summary>
        [JsonProperty]
        internal int ChangesSinceLastSink;

        protected IDisposable _unsubscriber;

        internal abstract IEnumerable<MetricRecord> FlushChangesSinceLastSink();

        internal abstract IEnumerable<MetricRecord> GetMetricRecords();

        /// <summary>
        /// Unsubscribes the tracker from the metric it is tracking.
        /// </summary>
        public void Unsubscribe()
        {
            _unsubscriber.Dispose();
        }

        internal abstract IMetric GetMetric();

        /// <summary>
        /// When new metric data is received, update the value and records so it reflects the new data.
        /// Called when Driver receives metrics from Evaluator.
        /// </summary>
        /// <param name="metric">Metric data received.</param>
        internal MetricTracker UpdateMetric(MetricTracker metric)
        {
            if (metric.ChangesSinceLastSink > 0)
            {
                var recordsToAdd = metric.GetMetricRecords();
                if (KeepUpdateHistory)
                {
                    foreach (MetricRecord record in recordsToAdd)
                    {
                        Records.Enqueue(record);
                    }
                }
                else
                {
                    Interlocked.Exchange(ref Records, (ConcurrentQueue<MetricRecord>)recordsToAdd);
                }
            }
            Interlocked.Add(ref ChangesSinceLastSink, metric.ChangesSinceLastSink);
            return this;
        }

        internal abstract MetricTracker Copy();
    }

    /// <summary>
    /// MetricData class maintains the current value of a single metric and keeps count of the number
    /// of times this metric has been updated. If the metric is immutable, it keeps a record of updates.
    /// Once the data has been processed, the records and count will reset.
    /// </summary>
    [JsonObject]
    public sealed class MetricTracker<T> : MetricTracker, ITracker<T>
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(MetricTracker<T>));

        [JsonProperty]
        private IMetric<T> Metric;

        /// <summary>
        /// Constructor for MetricData called when metric is registered.
        /// </summary>
        /// <param name="metric"></param>
        /// <param name="initialValue"></param>
        internal MetricTracker(IMetric<T> metric)
        {
            Subscribe(metric);
            ChangesSinceLastSink = 0;
            KeepUpdateHistory = metric.KeepUpdateHistory;
            Records = KeepUpdateHistory ? new ConcurrentQueue<MetricRecord>() : null;
            Records?.Enqueue(CreateMetricRecord(metric));
        }

        [JsonConstructor]
        internal MetricTracker(IMetric<T> metric, int changesSinceLastSink,
            IEnumerable<MetricRecord> records, bool keepUpdateHistory)
        {
            Metric = metric;
            Records = new ConcurrentQueue<MetricRecord>(records);
            KeepUpdateHistory = keepUpdateHistory;
            ChangesSinceLastSink = changesSinceLastSink;
        }

        internal override MetricTracker Copy()
        {
            return new MetricTracker<T>(Metric, ChangesSinceLastSink,
                FlushChangesSinceLastSink(), KeepUpdateHistory);
        }

        /// <summary>
        /// Flush records currently held in the records queue.
        /// </summary>
        /// <returns>A queue containing all the flushed records.</returns>
        internal override IEnumerable<MetricRecord> FlushChangesSinceLastSink()
        {
            ConcurrentQueue<MetricRecord> records = new ConcurrentQueue<MetricRecord>();
            if (Records != null)
            {
                while (Records.TryDequeue(out MetricRecord record))
                {
                    records.Enqueue(record);
                }
            }
            else
            {
                // Records will be empty only on eval side when tracker doesn't keep history.
                records.Enqueue(CreateMetricRecord(Metric));
            }
            Interlocked.Exchange(ref ChangesSinceLastSink, 0);
            return records;
        }

        /// <summary>
        /// Get the metric with its most recent value.
        /// </summary>
        /// <returns></returns>
        internal override IMetric GetMetric()
        {
            return Metric;
        }

        /// <summary>
        /// Get the metric with its most recent value.
        /// </summary>
        /// <returns></returns>
        internal IMetric<T> GetMetricTyped()
        {
            return Metric;
        }

        /// <summary>
        /// If KeepUpdateHistory is true, it will return all the records; otherwise, it will returen one record with the most recent value.
        /// </summary>
        /// <returns>The history of the metric records.</returns>
        internal override IEnumerable<MetricRecord> GetMetricRecords()
        {
            if (Records.IsEmpty)
            {
                return new List<MetricRecord>()
                {
                    CreateMetricRecord(Metric)
                };
            }
            else
            {
                return Records;
            }
        }

        /// <summary>
        /// Subscribes the tracker to a metric object.
        /// </summary>
        /// <param name="provider">The metric to track.</param>
        public void Subscribe(IMetric<T> provider)
        {
            Metric = provider;
            _unsubscriber = provider.Subscribe(this);
        }

        /// <summary>
        /// Creates and queues a new metric record with a value.
        /// </summary>
        /// <param name="value">Value of the new record.</param>
        public void Track(T value)
        {
            Interlocked.Increment(ref ChangesSinceLastSink);
            Records?.Enqueue(CreateMetricRecord(value));
        }

        private MetricRecord CreateMetricRecord(object metric)
        {
            return new MetricRecord(metric);
        }
    }
}
