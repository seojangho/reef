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
package org.apache.reef.javabridge.generic;

import org.apache.reef.bridge.JavaBridge;
import org.apache.reef.client.*;
import org.apache.reef.io.network.naming.NameServerConfiguration;
import org.apache.reef.javabridge.NativeInterop;
import org.apache.reef.runtime.yarn.driver.YarnDriverRestartConfiguration;
import org.apache.reef.tang.Configuration;
import org.apache.reef.tang.Configurations;
import org.apache.reef.tang.Tang;
import org.apache.reef.tang.annotations.Unit;
import org.apache.reef.tang.formats.AvroConfigurationSerializer;
import org.apache.reef.tang.formats.ConfigurationModule;
import org.apache.reef.util.EnvironmentUtils;
import org.apache.reef.util.logging.LoggingScope;
import org.apache.reef.util.logging.LoggingScopeFactory;
import org.apache.reef.wake.EventHandler;
import org.apache.reef.wake.MultiObserver;
import org.apache.reef.wake.avro.ProtocolSerializerNamespace;
import org.apache.reef.webserver.HttpHandlerConfiguration;
import org.apache.reef.webserver.HttpServerReefEventHandler;
import org.apache.reef.webserver.ReefEventStateManager;

import javax.inject.Inject;
import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * Clr Bridge Client.
 */
@Unit
public final class JobClient {

  /**
   * Standard java logger.
   */
  private static final Logger LOG = Logger.getLogger(JobClient.class.getName());

  /**
   * Reference to the REEF framework.
   * This variable is injected automatically in the constructor.
   */
  private final REEF reef;

  /**
   * A reference to the running job that allows client to send messages back to the job driver.
   */
  private RunningJob runningJob;

  /**
   * Set to false when job driver is done.
   */
  private boolean isBusy = true;

  private int driverMemory;

  private String driverId;

  private String jobSubmissionDirectory = "reefTmp/job_" + System.currentTimeMillis();

  /**
   * A factory that provides LoggingScope.
   */
  private final LoggingScopeFactory loggingScopeFactory;

  /**
   * Clr Bridge client.
   * Parameters are injected automatically by TANG.
   *
   * @param reef Reference to the REEF framework.
   */
  @Inject
  private JobClient(final REEF reef, final LoggingScopeFactory loggingScopeFactory) {
    this.loggingScopeFactory = loggingScopeFactory;
    this.reef = reef;
  }

  private static Configuration getNameServerConfiguration() {
    return NameServerConfiguration.CONF
        .set(NameServerConfiguration.NAME_SERVICE_PORT, 0)
        .build();
  }

  /**
   * @return the driver-side configuration to be merged into the DriverConfiguration to enable the HTTP server.
   */
  private static Configuration getHTTPConfiguration() {

    final Configuration httpHandlerConfiguration = HttpHandlerConfiguration.CONF
        .set(HttpHandlerConfiguration.HTTP_HANDLERS, HttpServerReefEventHandler.class)
        .build();

    final Configuration driverConfigurationForHttpServer = DriverServiceConfiguration.CONF
        .set(DriverServiceConfiguration.ON_EVALUATOR_ALLOCATED,
            ReefEventStateManager.AllocatedEvaluatorStateHandler.class)
        .set(DriverServiceConfiguration.ON_CONTEXT_ACTIVE, ReefEventStateManager.ActiveContextStateHandler.class)
        .set(DriverServiceConfiguration.ON_TASK_RUNNING, ReefEventStateManager.TaskRunningStateHandler.class)
        .set(DriverServiceConfiguration.ON_DRIVER_STARTED, ReefEventStateManager.StartStateHandler.class)
        .set(DriverServiceConfiguration.ON_DRIVER_STOP, ReefEventStateManager.StopStateHandler.class)
        .build();

    return Configurations.merge(httpHandlerConfiguration, driverConfigurationForHttpServer);
  }

  private static Configuration getYarnConfiguration() {

    final Configuration yarnDriverRestartConfiguration = YarnDriverRestartConfiguration.CONF
        .build();

    final Configuration driverRestartHandlerConfigurations = DriverRestartConfiguration.CONF
        .set(DriverRestartConfiguration.ON_DRIVER_RESTARTED,
            ReefEventStateManager.DriverRestartHandler.class)
        .set(DriverRestartConfiguration.ON_DRIVER_RESTART_TASK_RUNNING,
            ReefEventStateManager.DriverRestartTaskRunningStateHandler.class)
        .set(DriverRestartConfiguration.ON_DRIVER_RESTART_CONTEXT_ACTIVE,
            ReefEventStateManager.DriverRestartActiveContextStateHandler.class)
        .build();

    return Configurations.merge(yarnDriverRestartConfiguration, driverRestartHandlerConfigurations);
  }

  private Configuration getDriverConfiguration(final File folder) {

    final Configuration bridgeConfig = Tang.Factory.getTang().newConfigurationBuilder()
        .bindNamedParameter(ProtocolSerializerNamespace.class, "org.apache.reef.bridge.message")
        .bindImplementation(MultiObserver.class, JavaBridge.class)
        .build();

    ConfigurationModule driverConfigModule = DriverConfiguration.CONF
        .setMultiple(DriverConfiguration.GLOBAL_LIBRARIES, EnvironmentUtils.getAllClasspathJars())
        .set(DriverConfiguration.ON_EVALUATOR_ALLOCATED, JobDriver.AllocatedEvaluatorHandler.class)
        .set(DriverConfiguration.ON_EVALUATOR_FAILED, JobDriver.FailedEvaluatorHandler.class)
        .set(DriverConfiguration.ON_CONTEXT_ACTIVE, JobDriver.ActiveContextHandler.class)
        .set(DriverConfiguration.ON_CONTEXT_CLOSED, JobDriver.ClosedContextHandler.class)
        .set(DriverConfiguration.ON_CONTEXT_FAILED, JobDriver.FailedContextHandler.class)
        .set(DriverConfiguration.ON_CONTEXT_MESSAGE, JobDriver.ContextMessageHandler.class)
        .set(DriverConfiguration.ON_TASK_MESSAGE, JobDriver.TaskMessageHandler.class)
        .set(DriverConfiguration.ON_TASK_FAILED, JobDriver.FailedTaskHandler.class)
        .set(DriverConfiguration.ON_TASK_RUNNING, JobDriver.RunningTaskHandler.class)
        .set(DriverConfiguration.ON_TASK_COMPLETED, JobDriver.CompletedTaskHandler.class)
        .set(DriverConfiguration.ON_DRIVER_STARTED, JobDriver.StartHandler.class)
        .set(DriverConfiguration.ON_TASK_SUSPENDED, JobDriver.SuspendedTaskHandler.class)
        .set(DriverConfiguration.ON_EVALUATOR_COMPLETED, JobDriver.CompletedEvaluatorHandler.class)
        .set(DriverConfiguration.DRIVER_MEMORY, this.driverMemory)
        .set(DriverConfiguration.DRIVER_IDENTIFIER, this.driverId)
        .set(DriverConfiguration.DRIVER_JOB_SUBMISSION_DIRECTORY, this.jobSubmissionDirectory);

    try (final LoggingScope ls = this.loggingScopeFactory.getNewLoggingScope("JobClient::getDriverConfiguration")) {

      final File[] files = folder.listFiles();
      if (files != null) {
        for (final File f : files) {
          if (f.canRead() && f.exists() && f.isFile()) {
            driverConfigModule = driverConfigModule.set(DriverConfiguration.GLOBAL_FILES, f.getAbsolutePath());
          }
        }
      }

      final Path globalLibFile = Paths.get(NativeInterop.GLOBAL_LIBRARIES_FILENAME);
      if (!Files.exists(globalLibFile)) {
        LOG.log(Level.FINE, "Cannot find global classpath file at: {0}, assume there is none.",
            globalLibFile.toAbsolutePath());
      } else {
        try {
          final String globalLibString = new String(Files.readAllBytes(globalLibFile), StandardCharsets.UTF_8);
          for (final String fileName : globalLibString.split(",")) {
            driverConfigModule = driverConfigModule.set(
                DriverConfiguration.GLOBAL_LIBRARIES, new File(fileName).getPath());
          }
        } catch (final Exception e) {
          LOG.log(Level.WARNING,
              "Cannot read global libraries from file: " + globalLibFile.toAbsolutePath(), e);
        }
      }

      return Configurations.merge(
          driverConfigModule.build(), bridgeConfig, getHTTPConfiguration(), getNameServerConfiguration());
    }
  }

  /**
   * Launch the job driver.
   *
   * @throws org.apache.reef.tang.exceptions.BindException configuration error.
   */
  public void submit(final File clrFolder, final boolean submitDriver,
                     final boolean local, final Configuration clientConfig) {

    try (final LoggingScope ls = this.loggingScopeFactory.driverSubmit(submitDriver)) {

      Configuration driverConfig = getDriverConfiguration(clrFolder);
      if (!local) {
        driverConfig = Configurations.merge(driverConfig, getYarnConfiguration());
      }

      if (submitDriver) {
        this.reef.submit(driverConfig);
      } else {
        final File driverConfigFile = new File(System.getProperty("user.dir") + "/driver.config");
        try {
          new AvroConfigurationSerializer().toFile(
              Configurations.merge(driverConfig, clientConfig), driverConfigFile);
          LOG.log(Level.INFO, "Driver configuration file created at " + driverConfigFile.getAbsolutePath());
        } catch (final IOException e) {
          throw new RuntimeException(
              "Cannot create driver configuration file at " + driverConfigFile.getAbsolutePath(), e);
        }
      }
    }
  }

  /**
   * Set the driver memory.
   */
  @SuppressWarnings("checkstyle:hiddenfield")
  public void setDriverInfo(final String identifier, final int memory, final String jobSubmissionDirectory) {
    if (identifier == null || identifier.isEmpty()) {
      throw new RuntimeException("driver id cannot be null or empty");
    }
    if (memory <= 0) {
      throw new RuntimeException("driver memory cannot be negative number: " + memory);
    }
    this.driverMemory = memory;
    this.driverId = identifier;
    if (jobSubmissionDirectory != null && !jobSubmissionDirectory.equals("empty")) {
      this.jobSubmissionDirectory = jobSubmissionDirectory;
    } else {
      LOG.log(Level.FINE,
          "No job submission directory provided by CLR user, will use {0}", this.jobSubmissionDirectory);
    }
  }

  /**
   * Notify the process in waitForCompletion() method that the main process has finished.
   */
  private synchronized void stopAndNotify() {
    this.runningJob = null;
    this.isBusy = false;
    this.notify();
  }

  /**
   * Wait for the job driver to complete.
   */
  public void waitForCompletion(final int waitTime) {
    LOG.info("Waiting for the Job Driver to complete: " + waitTime);
    if (waitTime == 0) {
      close(0);
      return;
    } else if (waitTime < 0) {
      waitTillDone();
    }
    final long endTime = System.currentTimeMillis() + waitTime * 1000;
    close(endTime);
  }

  public void close(final long endTime) {
    while (endTime > System.currentTimeMillis()) {
      try {
        Thread.sleep(1000);
      } catch (final InterruptedException e) {
        LOG.log(Level.SEVERE, "Thread sleep failed");
      }
    }
    LOG.log(Level.INFO, "Done waiting.");
    this.stopAndNotify();
    reef.close();
  }

  private void waitTillDone() {
    while (this.isBusy) {
      try {
        synchronized (this) {
          this.wait();
        }
      } catch (final InterruptedException ex) {
        LOG.log(Level.WARNING, "Waiting for result interrupted.", ex);
      }
    }
    this.reef.close();
  }

  /**
   * Receive notification from the job driver that the job had failed.
   */
  final class FailedJobHandler implements EventHandler<FailedJob> {
    @Override
    public void onNext(final FailedJob job) {
      LOG.log(Level.SEVERE, "Failed job: " + job.getId(), job.getMessage());
      stopAndNotify();
    }
  }

  /**
   * Receive notification from the job driver that the job had completed successfully.
   */
  final class CompletedJobHandler implements EventHandler<CompletedJob> {
    @Override
    public void onNext(final CompletedJob job) {
      LOG.log(Level.INFO, "Completed job: {0}", job.getId());
      stopAndNotify();
    }
  }

  /**
   * Receive notification that there was an exception thrown from the job driver.
   */
  final class RuntimeErrorHandler implements EventHandler<FailedRuntime> {
    @Override
    public void onNext(final FailedRuntime error) {
      LOG.log(Level.SEVERE, "Error in job driver: " + error, error.getMessage());
      stopAndNotify();
    }
  }

  final class WakeErrorHandler implements EventHandler<Throwable> {
    @Override
    public void onNext(final Throwable error) {
      LOG.log(Level.SEVERE, "Error communicating with job driver, exiting... ", error);
      stopAndNotify();
    }
  }
}
