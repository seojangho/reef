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

namespace Org.Apache.REEF.Common.Telemetry
{
    /// <summary>
    /// Metric interface. A generic interface for individual metrics.
    /// </summary>
    public interface IMetric
    {
        /// <summary>
        /// Name of the metric.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of the metric.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Value of the metric, stored as object.
        /// </summary>
        object ValueUntyped { get; }

        /// <summary>
        /// Flag for the immutability of the metric. 
        /// </summary>
        bool KeepUpdateHistory { get; }
    }

    public interface IMetric<T> : IMetric
    {
        /// <summary>
        /// Value of the metric.
        /// </summary>
        T Value { get; }

        /// <summary>
        /// Assign a tracker to track the metric.
        /// </summary>
        /// <param name="tracker">The metric tracker assigned to track updates.</param>
        /// <returns></returns>
        IDisposable Subscribe(ITracker<T> tracker);
    }
}
