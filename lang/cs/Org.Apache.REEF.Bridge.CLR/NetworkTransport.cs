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
using System.Threading;
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
    /// The CLR Bridge Network class aggregates a RemoteManager and
    /// Protocol Serializer to provide a simple send/receive interface
    /// between the CLR and Java bridges.
    /// </summary>
    public sealed class NetworkTransport : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(NetworkTransport));

        private readonly BlockingCollection<Tuple<long, object>>
            _sendQueue = new BlockingCollection<Tuple<long, object>>();

        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private readonly CancellationToken _cancelToken;
        private readonly Thread _writer;

        private readonly ProtocolSerializer _serializer;
        private readonly IObserver<byte[]> _remoteObserver;
        private readonly REEFFileNames _fileNames;

        /// <summary>
        /// Construct a network stack using the wake remote manager.
        /// </summary>
        /// <param name="localAddressProvider">An address provider used to obtain
        /// a local IP address on an open port.</param>
        /// <param name="serializer">Serializer/deserializer of the bridge messages.</param>
        /// <param name="localObserver">Handler of the incoming bridge messages.</param>
        /// <param name="remoteManagerFactory">RemoteManager factory.
        /// We need a new instance of the RM to communicate with the Java side of the bridge.</param>
        /// <param name="fileNames">Collection of global constants for file paths and such.</param>
        [Inject]
        private NetworkTransport(
            ILocalAddressProvider localAddressProvider,
            ProtocolSerializer serializer,
            LocalObserver localObserver,
            IRemoteManagerFactory remoteManagerFactory,
            REEFFileNames fileNames)
        {
            _serializer = serializer;
            _fileNames = fileNames;

            // Instantiate the remote manager.
            IRemoteManager<byte[]> remoteManager =
                remoteManagerFactory.GetInstance(localAddressProvider.LocalAddress, new ByteCodec());

            // Listen to the java bridge on the local end point.
            remoteManager.RegisterObserver(localObserver);
            Logger.Log(Level.Info, "Local observer listening to java bridge on: [{0}]", remoteManager.LocalEndpoint);

            // Instantiate a remote observer to send messages to the java bridge.
            IPEndPoint javaIpEndPoint = GetJavaBridgeEndpoint();
            Logger.Log(Level.Info, "Connecting to java bridge on: [{0}]", javaIpEndPoint);
            _remoteObserver = remoteManager.GetRemoteObserver(javaIpEndPoint);

            // Initialize and start the message writer thread.
            _cancelToken = _tokenSource.Token;
            _writer = new Thread(WriteMessage);
            _writer.Start();

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
            Logger.Log(Level.Verbose, "Sending message: {0} :: {1}", identifier, message);
            _sendQueue.Add(new Tuple<long, object>(identifier, message));
        }

        /// <summary>
        /// Retrieves the address of the java bridge.
        /// </summary>
        /// <returns>IP address and port of the Java bridge.</returns>
        private IPEndPoint GetJavaBridgeEndpoint()
        {
            string javaBridgeAddress = File.ReadAllText(_fileNames.DriverJavaBridgeEndpointFileName);
            Logger.Log(Level.Info, "Java bridge address: {0}", javaBridgeAddress);

            string[] javaAddressStrs = javaBridgeAddress.Split(':');
            IPAddress javaBridgeIpAddress = IPAddress.Parse(javaAddressStrs[0]);
            int port = int.Parse(javaAddressStrs[1]);

            return new IPEndPoint(javaBridgeIpAddress, port);
        }

        /// <summary>
        /// Stop the internal writer thread.
        /// </summary>
        public void Dispose()
        {
            try
            {
                _tokenSource.Cancel();
                _writer.Join();
                _tokenSource.Dispose();
            }
            catch (Exception e)
            {
                Logger.Log(Level.Error, "Disposing of writer thread", e);
            }
        }

        /// <summary>
        /// Write messages on the send queue to the network.
        /// </summary>
        void WriteMessage()
        {
            while (!_cancelToken.IsCancellationRequested)
            {
                var item = _sendQueue.Take(_cancelToken);
                _remoteObserver.OnNext(_serializer.Write(item.Item2, item.Item1));
            }
        }
    }
}
