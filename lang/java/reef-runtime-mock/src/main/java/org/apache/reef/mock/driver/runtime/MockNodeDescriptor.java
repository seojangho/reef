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
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

package org.apache.reef.mock.driver.runtime;

import org.apache.reef.annotations.Unstable;
import org.apache.reef.annotations.audience.Private;
import org.apache.reef.driver.catalog.NodeDescriptor;
import org.apache.reef.driver.catalog.RackDescriptor;

import java.net.InetSocketAddress;
import java.util.ArrayList;
import java.util.List;

/**
 * mock node descriptor.
 */
@Unstable
@Private
public final class MockNodeDescriptor implements NodeDescriptor {
  @Override
  public InetSocketAddress getInetSocketAddress() {
    throw new UnsupportedOperationException();
  }

  @Override
  public RackDescriptor getRackDescriptor() {
    return new RackDescriptor() {
      @Override
      public List<NodeDescriptor> getNodes() {
        final List<NodeDescriptor> nodes = new ArrayList<>();
        nodes.add(MockNodeDescriptor.this);
        return nodes;
      }

      @Override
      public String getName() {
        return "mock";
      }
    };
  }

  @Override
  public String getName() {
    return "mock";
  }

  @Override
  public String getId() {
    return "mock";
  }
}
