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
using Org.Apache.REEF.Bridge;
using org.apache.reef.bridge.message;
using Org.Apache.REEF.Tang.Annotations;
using Org.Apache.REEF.Utilities.Logging;
using Org.Apache.REEF.Wake.Avro;
using System.Threading;

namespace Org.Apache.REEF.Driver.Bridge
{
    /// <summary>
    /// An Observer implementation which handles all of the messages defined in
    /// the Java to C# Avro protocol coming from the Java bridge which invokes
    /// the appropriate target method in the C# side of the driver.
    /// </summary>
    internal sealed class ClrBridge : IObserver<IMessageInstance<SystemOnStart>>
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(ClrBridge));
        ////private static long identifierSource = 0;
        private readonly NetworkTransport network;
        internal DriverBridge driverBridge { get; set; }

        /// <summary>
        /// Inject the Clr bridge with the network for communicating with the Java bridge.
        /// </summary>
        /// <param name="network">The CLR-Java network to be used by the ClrBridge.</param>
        [Inject]
        private ClrBridge(NetworkTransport network)
        {
            this.network = network;
        }

        /// <summary>
        /// Callback to process the SystemOnStart message from the Java side of the bridge.
        /// </summary>
        /// <param name="systemOnStart">Avro message from java indicating the system is starting.</param>
        public void OnNext(IMessageInstance<SystemOnStart> systemOnStart)
        {
            ////Logger.Log(Level.Info, "SystemOnStart message received {0}", systemOnStart.Sequence);

            ////// Convert Java time to C# time.
            ////// Java - millisecs, C# - 100 nanosecs (muliply by 10000)
            ////// Java - Jan 1, 1970, C# - Jan 1, 0001 (add 1969 years)
            ////DateTime startTime = new DateTime(10000 * systemOnStart.Message.dateTime);
            ////startTime = startTime.AddYears(1969);
            ////Logger.Log(Level.Info, "Start time is {0}", startTime);

            ////driverBridge.StartHandlersOnNext(startTime);
            ////long identifier = Interlocked.Increment(ref identifierSource);
            ////network.Send(identifier, new Acknowledgement(systemOnStart.Sequence));
        }

        /// <summary>
        /// Handles error conditions in the bridge network.
        /// </summary>
        /// <param name="error">The exception generated in the transport layer.</param>
        public void OnError(Exception error)
        {
            Logger.Log(Level.Error, "ClrBridge error: ", error);
        }

        /// <summary>
        /// Notification that no nore message processing is required.
        /// </summary
        public void OnCompleted()
        {
            Logger.Log(Level.Info, "ClrBridge OnCompleted");
        }
    }
}