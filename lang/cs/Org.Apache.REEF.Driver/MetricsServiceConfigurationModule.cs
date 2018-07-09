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
using Org.Apache.REEF.Common.Context;
using Org.Apache.REEF.Common.Telemetry;
using Org.Apache.REEF.Driver.Bridge;
using Org.Apache.REEF.Tang.Formats;
using Org.Apache.REEF.Tang.Util;

namespace Org.Apache.REEF.Driver
{
    /// <summary>
    /// Configuration module for MetricsService.
    /// </summary>
    public sealed class MetricsServiceConfigurationModule : ConfigurationModuleBuilder
    {
        public static readonly OptionalImpl<IMetricsSink> OnMetricsSink = new OptionalImpl<IMetricsSink>();
        public static readonly OptionalParameter<int> MetricSinkThreshold = new OptionalParameter<int>();

        /// <summary>
        /// It provides the configuration for MetricsService
        /// </summary>
        public static ConfigurationModule ConfigurationModule = new MetricsServiceConfigurationModule()
            .BindSetEntry<DriverBridgeConfigurationOptions.ContextMessageHandlers, MetricsService, IObserver<IContextMessage>>(
                GenericType<DriverBridgeConfigurationOptions.ContextMessageHandlers>.Class,
                GenericType<MetricsService>.Class)
            .BindSetEntry(GenericType<MetricSinks>.Class, OnMetricsSink)
            .BindNamedParameter(GenericType<MetricSinkThreshold>.Class, MetricSinkThreshold)
            .Build();
    }
}
