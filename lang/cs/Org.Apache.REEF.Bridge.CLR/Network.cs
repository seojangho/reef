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
using System.IO;
using System.Net;
using org.apache.reef.bridge.message;
using Org.Apache.REEF.Common.Files;
using Org.Apache.REEF.Tang.Annotations;
using Org.Apache.REEF.Utilities.Logging;
using Org.Apache.REEF.Wake.Avro;
using Org.Apache.REEF.Wake.Remote;
using Org.Apache.REEF.Wake.Remote.Impl;

namespace Org.Apache.REEF.Bridge
{
    /// <summary>
    /// The CLR Bridge Network class agregates a RemoteManager and
    /// Protocol Serializer to provide a simple send/receive interface
    /// between the CLR and Java bridges. 
    /// </summary>
    public sealed class Network
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(Network));
        private readonly ProtocolSerializer serializer;
        private readonly BlockingCollection<byte[]> queue = new BlockingCollection<byte[]>();
        private readonly IRemoteManager<byte[]> remoteManager;
        private readonly IObserver<byte[]> remoteObserver;
        private readonly REEFFileNames fileNames;

        /// <summary>
        /// Construct a network stack using the wake remote manager.
        /// </summary>
        /// <param name="localAddressProvider">An address provider used to obtain a local IP address on an open port.</param>
        /// <param name="serializer"></param>
        /// <param name="localObserver"></param>
        [Inject]
        public Network(
            ILocalAddressProvider localAddressProvider,
            ProtocolSerializer serializer,
            LocalObserver localObserver,
            IRemoteManagerFactory remoteManagerFactory,
            REEFFileNames fileNames)
        {
            this.serializer = serializer;
            this.fileNames = fileNames;

            // Get the path to the bridge name server endpoint file.
            string javaBridgeAddress = GetJavaBridgeAddress();

            // Instantiate the remote manager.
            remoteManager = remoteManagerFactory.GetInstance(localAddressProvider.LocalAddress, new ByteCodec());

            // Listen to the java bridge on the local end point.
            remoteManager.RegisterObserver(localObserver);
            Logger.Log(Level.Info, "Local observer listening to java bridge on: [{0}]", remoteManager.LocalEndpoint);

            // Instantiate a remote observer to send messages to the java bridge.
            string[] javaAddressStrs = javaBridgeAddress.Split(':');
            IPAddress javaBridgeIpAddress = IPAddress.Parse(javaAddressStrs[0]);
            int port = int.Parse(javaAddressStrs[1]);
            IPEndPoint javaIpEndPoint = new IPEndPoint(javaBridgeIpAddress, port);
            Logger.Log(Level.Info, "Connecting to java bridge on: [{0}]", javaIpEndPoint);
            remoteObserver = remoteManager.GetRemoteObserver(javaIpEndPoint);

            // Negotiate the protocol.
            Send(0, new BridgeProtocol(100));
        }

        /// <summary>
        /// Send a message to the java side of the bridge.
        /// </summary>
        /// <param name="identifier">A long value that which is the unique sequence identifier of the message.</param>
        /// <param name="message">An object reference to a message in the org.apache.reef.bridge.message package.</param>
        public void Send(long identifier, object message)
        {
            Logger.Log(Level.Info, "Sending message: {0}", message);
            remoteObserver.OnNext(serializer.Write(message, identifier));
        }

        /// <summary>
        /// Retrieves the address of the java bridge.
        /// </summary>
        /// <returns>A string containing the IP address and port of the Java bridge.</returns>
        private string GetJavaBridgeAddress()
        {
            using (FileStream stream = File.Open(fileNames.GetDriverJavaBridgeEndpoint(), FileMode.Open))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string javaBridgeAddress = reader.ReadToEnd();
                    Logger.Log(Level.Info, "Java bridge address: {0}", javaBridgeAddress);
                    return javaBridgeAddress;
                }
            }
        }
    }
}
