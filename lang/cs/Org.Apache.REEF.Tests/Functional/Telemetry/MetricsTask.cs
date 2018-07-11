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
using System.Threading;
using Org.Apache.REEF.Common.Tasks;
using Org.Apache.REEF.Common.Telemetry;
using Org.Apache.REEF.Tang.Annotations;
using Org.Apache.REEF.Utilities.Logging;

namespace Org.Apache.REEF.Tests.Functional.Telemetry
{
    /// <summary>
    /// A test task that is to add counter information during the task execution.
    /// </summary>
    public class MetricsTask : ITask
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(MetricsTask));

        public const string TestCounter = "TestCounter";
        public const string TestIntMetric = "Iterations";

        private readonly IEvaluatorMetrics _evaluatorMetrics;

        public CounterMetric metric1;
        public IntegerMetric metric2;

        [Inject]
        private MetricsTask(IEvaluatorMetrics evaluatorMetrics)
        {
            _evaluatorMetrics = evaluatorMetrics;
            metric1 = _evaluatorMetrics.CreateAndRegisterMetric<CounterMetric, int>(TestCounter, TestCounter + " description", false);
            metric2 = _evaluatorMetrics.CreateAndRegisterMetric<IntegerMetric, int>(TestIntMetric, TestIntMetric + " description", true);
        }

        public byte[] Call(byte[] memento)
        {
            for (int i = 1; i <= 2000; i++)
            {
                metric1.Increment();
                metric2.AssignNewValue(i);
                Thread.Sleep(10);
            }
            return null;
        }

        public void Dispose()
        {
        }
    }
}
