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
using Newtonsoft.Json;

namespace Org.Apache.REEF.Common.Telemetry
{
    /// <summary>
    /// Base implementation with a generic value type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class MetricBase<T> : IMetric<T>
    {
        protected T _typedValue;

        protected ITracker<T> _tracker;

        public string Name
        {
            get; internal set;
        }

        public string Description
        {
            get; internal set;
        }

        public bool KeepUpdateHistory
        {
            get; internal set;
        }

        public object ValueUntyped
        {
            get { return _typedValue; }
        }

        public T Value
        {
            get { return _typedValue; }
        }

        public MetricBase()
        {
            _typedValue = default;
        }

        public MetricBase(string name, string description, bool keepUpdateHistory = true)
        {
            Name = name;
            Description = description;
            KeepUpdateHistory = keepUpdateHistory;
            _typedValue = default;
        }

        [JsonConstructor]
        public MetricBase(string name, string description, T value, bool keepUpdateHistory)
        {
            Name = name;
            Description = description;
            KeepUpdateHistory = keepUpdateHistory;
            _typedValue = value;
        }

        /// <summary>
        /// Assign and track the new value to metric.
        /// In most cases, this method should be overridden in derived classes using Interlocked.
        /// </summary>
        /// <param name="value">Value to assign the metric.</param>
        public abstract void AssignNewValue(T value);

        public IDisposable Subscribe(ITracker<T> tracker)
        {
            _tracker = tracker;
            return new Unsubscriber(tracker);
        }

        private class Unsubscriber : IDisposable
        {
            private ITracker<T> _tracker;

            public Unsubscriber(ITracker<T> tracker)
            {
                _tracker = tracker;
            }

            public void Dispose()
            {
                _tracker = null;
            }
        }
    }
}
