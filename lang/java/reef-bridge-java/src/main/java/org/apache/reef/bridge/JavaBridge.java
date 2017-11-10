/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
package org.apache.reef.bridge;

import org.apache.reef.bridge.message.Acknowledgement;
import org.apache.reef.bridge.message.BridgeProtocol;
import org.apache.reef.bridge.message.SetupBridge;
import org.apache.reef.bridge.message.SystemOnStart;
import org.apache.reef.javabridge.BridgeHandlerManager;
import org.apache.reef.javabridge.EvaluatorRequestorBridge;
import org.apache.reef.util.MultiAsyncToSync;
import org.apache.reef.util.exception.InvalidIdentifierException;
import org.apache.reef.wake.EventHandler;
import org.apache.reef.wake.impl.MultiObserverImpl;
import org.apache.reef.wake.time.event.StartTime;
import org.apache.reef.wake.time.runtime.Timer;

import javax.inject.Inject;
import java.net.InetSocketAddress;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicLong;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * Implements the Avro message protocol between the Java and C# bridges.
 */
public final class JavaBridge extends MultiObserverImpl {

  private static final Logger LOG = Logger.getLogger(JavaBridge.class.getName());

  private final MultiAsyncToSync blocker = new MultiAsyncToSync(20, TimeUnit.SECONDS);
  private final AtomicLong idCounter = new AtomicLong(0);
  private final NetworkTransport network;
  private boolean isProtocolEstablished = false;
  private final Timer timer;
  private EventHandler<StartTime> initializedHander;
  private StartTime startTime;

  /**
   * Implements the RPC interface to the C# side of the bridge.
   */
  @Inject
  public JavaBridge(final NetworkTransport network, final Timer timer) {
    this.network = network;
    this.timer = timer;
  }

  /**
   * Retrieves the internet socket address of the Java side of the bridge.
   * @return An InetSocketAddress which contains the address of the Java
   * side of the bridge.
   */
  public InetSocketAddress getAddress() {
    return network.getAddress();
  }

  /**
   * Indicates whether or the not bridge messaging protocol between has been negotiated.
   * @return true after the two sides of the bridge have established the messaging
   * protocol; otherwise; false.
   */
  public synchronized boolean isProtocolEstablished() {
    return isProtocolEstablished;
  }

  /**
   *
   * @param initializedHander
   * @param startTime
   */
  public synchronized void onInitializedHandler(
          final EventHandler<StartTime> initializedHander, StartTime startTime) {
    this.initializedHander = initializedHander;
    this.startTime = startTime;
    if (isProtocolEstablished()) {
      this.initializedHander.onNext(startTime);
    }
  }

  /**
   * Called when an error occurs in the MultiObserver base class.
   * @param error An exception reference that contains the error
   *              which occurred
   */
  public void onError(final Exception error) {
    LOG.log(Level.SEVERE, "Error received by Java bridge: ", error);
  }

  /**
   * Called when no more message processing is required.
   */
  public void onCompleted() {
    LOG.log(Level.FINE, "OnCompleted");
  }

  /**
   * Processes protocol messages from the C# side of the bridge.
   * @param identifier A long value which is the unique message identifier.
   * @param protocol A reference to the received Avro protocol message.
   */
  public synchronized void onNext(final long identifier, final BridgeProtocol protocol)
        throws InvalidIdentifierException, InterruptedException{
    isProtocolEstablished = true;
    if (initializedHander != null) {
      initializedHander.onNext(startTime);
    }
    LOG.log(Level.FINEST, "Received protocol message: [{0}] {1}", new Object[] {identifier, protocol.getOffset()});
  }

  /**
   * Releases the caller sleeping on the inpput acknowledgement message.
   * @param identifier A long value which is the unique message identifier.
   * @param acknowledgement The incoming acknowledgement message whose call will be released.
   * @throws InvalidIdentifierException The call identifier is invalid.
   * @throws InterruptedException Thread was interrupted by another thread.
   */
  public void onNext(final long identifier, final Acknowledgement acknowledgement)
        throws InvalidIdentifierException, InterruptedException {
    LOG.log(Level.FINEST, "Received acknowledgement message for id = [{0}]", identifier);
    blocker.release(acknowledgement.getMessageIdentifier());
  }

   /**
   * Sends a SetupBridge message to the CLR bridge and blocks the caller
   * until an acknowledgement message is received.
   *
   * @param httpPortNumber
   * @param handlerManager
   * @param javaEvaluatorRequestorBridge
   */
  public void callClrSystemSetupBridgeHandlerManager(final String httpPortNumber,
        final BridgeHandlerManager handlerManager, final EvaluatorRequestorBridge javaEvaluatorRequestorBridge)
          throws InvalidIdentifierException, InterruptedException {

    final long identifier = idCounter.getAndIncrement();
    LOG.log(Level.FINE, "clrSystemSetupBridgeHandlerManager called with id [{0}]", identifier);

    blocker.block(identifier, new Runnable() {
      @Override
      public void run() {
        final SetupBridge msgSetupBridge = new SetupBridge(httpPortNumber);
        LOG.log(Level.FINE, "Send setup bridge message [{0}] :: {1}", new Object[]{identifier, msgSetupBridge});
        network.send(identifier, msgSetupBridge);
      }
    });
  }

  /**
   * Sends a SystemOnStart message to the CLR bridge and blocks the caller
   * until an acknowledgement message is received.
   */
  public void callClrSystemOnStartHandler() throws InvalidIdentifierException, InterruptedException {
    final long identifier = idCounter.getAndIncrement();
    LOG.log(Level.FINE, "callClrSystemOnStartHandler called with id [{0}]", identifier);

    blocker.block(identifier, new Runnable() {
      @Override
      public void run() {
        final SystemOnStart msgStart = new SystemOnStart(timer.getCurrent() / 1000);
        LOG.log(Level.FINE, "Send start message [{0}] :: {1}", new Object[]{identifier, msgStart});
        network.send(identifier, msgStart);
      }
    });
  }
}
