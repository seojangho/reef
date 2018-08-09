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
package org.apache.reef.bridge.driver.launch.azbatch;

import org.apache.reef.annotations.audience.Private;
import org.apache.reef.bridge.driver.launch.RuntimeConfigurationProvider;
import org.apache.reef.bridge.proto.ClientProtocol;
import org.apache.reef.runtime.azbatch.client.AzureBatchRuntimeConfiguration;
import org.apache.reef.runtime.azbatch.client.AzureBatchRuntimeConfigurationCreator;
import org.apache.reef.tang.Configuration;

import javax.inject.Inject;

/**
 * Azure batch runtime configuration provider.
 */
@Private
public final class AzureBatchConfigurationProvider implements RuntimeConfigurationProvider {

  @Inject
  private AzureBatchConfigurationProvider() {
  }

  public Configuration getRuntimeConfiguration(
      final ClientProtocol.DriverClientConfiguration driverClientConfiguration) {
    return generateConfigurationFromJobSubmissionParameters(driverClientConfiguration);
  }

  private static Configuration generateConfigurationFromJobSubmissionParameters(
      final ClientProtocol.DriverClientConfiguration driverClientConfiguration) {
    return AzureBatchRuntimeConfigurationCreator.getOrCreateAzureBatchRuntimeConfiguration(
        driverClientConfiguration.getOperatingSystem() ==
            ClientProtocol.DriverClientConfiguration.OS.WINDOWS)
        .set(AzureBatchRuntimeConfiguration.AZURE_BATCH_ACCOUNT_NAME,
            driverClientConfiguration.getAzbatchRuntime().getAzureBatchAccountName())
        .set(AzureBatchRuntimeConfiguration.AZURE_BATCH_ACCOUNT_KEY,
            driverClientConfiguration.getAzbatchRuntime().getAzureBatchAccountKey())
        .set(AzureBatchRuntimeConfiguration.AZURE_BATCH_ACCOUNT_URI,
            driverClientConfiguration.getAzbatchRuntime().getAzureBatchAccountUri())
        .set(AzureBatchRuntimeConfiguration.AZURE_BATCH_POOL_ID,
            driverClientConfiguration.getAzbatchRuntime().getAzureBatchPoolId())
        .set(AzureBatchRuntimeConfiguration.AZURE_STORAGE_ACCOUNT_NAME,
            driverClientConfiguration.getAzbatchRuntime().getAzureStorageAccountName())
        .set(AzureBatchRuntimeConfiguration.AZURE_STORAGE_ACCOUNT_KEY,
            driverClientConfiguration.getAzbatchRuntime().getAzureStorageAccountKey())
        .set(AzureBatchRuntimeConfiguration.AZURE_STORAGE_CONTAINER_NAME,
            driverClientConfiguration.getAzbatchRuntime().getAzureStorageContainerName())
        .build();
  }
}
