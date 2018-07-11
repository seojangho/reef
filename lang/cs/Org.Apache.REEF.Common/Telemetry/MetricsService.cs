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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Org.Apache.REEF.Common.Context;
using Org.Apache.REEF.Tang.Annotations;
using Org.Apache.REEF.Utilities;
using Org.Apache.REEF.Utilities.Attributes;
using Org.Apache.REEF.Utilities.Logging;

namespace Org.Apache.REEF.Common.Telemetry
{
    /// <summary>
    /// Metrics Service that handles metrics from the Evaluator and Driver.
    /// </summary>
    internal sealed class MetricsService : IObserver<IContextMessage>, IObserver<IDriverMetrics>
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(MetricsService));

        /// <summary>
        /// The set of metrics Metrics Service maintains.
        /// </summary>
        private readonly MetricsData _metricsData;

        /// <summary>
        /// A set of Metric Sinks.
        /// </summary>
        private readonly ISet<IMetricsSink> _metricsSinks;

        /// <summary>
        /// The total number of changes that has to be met to trigger the sinks.
        /// </summary>
        private readonly int _metricSinkThreshold;

        /// <summary>
        /// It can be bound with driver configuration as a context message handler
        /// </summary>
        [Inject]
        private MetricsService(
            [Parameter(typeof(MetricSinks))] ISet<IMetricsSink> metricsSinks,
            [Parameter(typeof(MetricSinkThreshold))] int metricSinkThreshold,
            MetricsData metricsData)
        {
            _metricsSinks = metricsSinks;
            _metricSinkThreshold = metricSinkThreshold;
            _metricsData = metricsData;
        }

        /// <summary>
        /// Called whenever context message is received
        /// </summary>
        /// <param name="contextMessage">Serialized EvaluatorMetrics</param>
        public void OnNext(IContextMessage contextMessage)
        {
            var msgReceived = ByteUtilities.ByteArraysToString(contextMessage.Message);
            var evalMetrics = new EvaluatorMetrics(msgReceived);
            var metricsData = evalMetrics.GetMetricsData();

            Logger.Log(Level.Info, "Received {0} metrics with context message of length {1}",
                metricsData.GetMetricTrackers().Count(), msgReceived.Length);

            _metricsData.Update(metricsData);

            if (_metricsData.TriggerSink(_metricSinkThreshold))
            {
                Sink(_metricsData.FlushMetricRecords());
            }
        }

        /// <summary>
        /// Call each Sink to process the cached metric records.
        /// </summary>
        private void Sink(IEnumerable<KeyValuePair<string, MetricRecord>> metricRecords)
        {
            foreach (var s in _metricsSinks)
            {
                try
                {
                    Task.Run(() => s.Sink(metricRecords));
                }
                catch (Exception e)
                {
                    Logger.Log(Level.Error, "Exception in Sink " + s.GetType().AssemblyQualifiedName, e);
                }
                finally
                {
                    s.Dispose();
                }
            }
        }

        /// <summary>
        /// Called when task is completed to sink cached metrics.
        /// </summary>
        public void OnCompleted()
        {
            Sink(_metricsData.FlushMetricRecords());
            Logger.Log(Level.Info, "Completed");
        }

        public void OnError(Exception error)
        {
            Logger.Log(Level.Error, "MetricService error", error);
        }

        /// <summary>
        /// Observer of IDriverMetrics.
        /// Called when Driver metrics data is changed.
        /// </summary>
        /// <param name="driverMetrics">driver metrics data.</param>
        public void OnNext(IDriverMetrics driverMetrics)
        {
            Sink(driverMetrics.GetMetricsData().FlushMetricRecords());
        }
    }
}
