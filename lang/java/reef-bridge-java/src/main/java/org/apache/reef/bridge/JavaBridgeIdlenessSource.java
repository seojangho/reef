/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
package org.apache.reef.bridge;

import org.apache.reef.runtime.common.driver.idle.DriverIdlenessSource;
import org.apache.reef.runtime.common.driver.idle.IdleMessage;
import org.apache.reef.tang.InjectionFuture;
import javax.inject.Inject;


/**
 * Notifies the driver that the system is not idle when a SystemOnStart message is
 * pending or the bridge protocol has not been negotiated.
 */
public final class JavaBridgeIdlenessSource implements DriverIdlenessSource {
  private final IdleMessage notIdleMessage =
      new IdleMessage("JavaBridge", "SystemOnStart is pending.", false);
  private final IdleMessage idleMessage =
      new IdleMessage("JavaBridge", "No events pending", true);
  private final InjectionFuture<JavaBridge> bridge;

  @Inject
  private JavaBridgeIdlenessSource(final InjectionFuture<JavaBridge> bridge) {
    this.bridge = bridge;
  }

  /**
   * Provides the proper idle message based on the current JavaBridge state.
   * @return An IdleMessage instance that reflects a non-idle state when
   *         a SystemOnStart message is pending or the bridge messaging protocol
   *         has not been established.
   */
  @Override
  public IdleMessage getIdleStatus() {
    if (!bridge.get().isProtocolEstablished() || bridge.get().isSystemOnStartPending()) {
      return notIdleMessage;
    } else {
      return idleMessage;
    }
  }
}
