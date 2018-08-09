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
using System.Globalization;
using System.Threading;
using Org.Apache.REEF.Common.Protobuf.ReefProtocol;
using Org.Apache.REEF.Common.Tasks;
using Org.Apache.REEF.Common.Tasks.Events;
using Org.Apache.REEF.Tang.Annotations;
using Org.Apache.REEF.Tang.Implementations.InjectionPlan;
using Org.Apache.REEF.Utilities;
using Org.Apache.REEF.Utilities.Attributes;
using Org.Apache.REEF.Utilities.Logging;

namespace Org.Apache.REEF.Common.Runtime.Evaluator.Task
{
    internal sealed class TaskRuntime : IObserver<ICloseEvent>, IObserver<ISuspendEvent>, IObserver<IDriverMessage>
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(TaskRuntime));

        private readonly TaskStatus _currentStatus;
        private readonly Optional<IDriverConnectionMessageHandler> _driverConnectionMessageHandler;
        private readonly Optional<IDriverMessageHandler> _driverMessageHandler;
        private readonly ITask _userTask;
        private readonly IInjectionFuture<IObserver<ISuspendEvent>> _suspendHandlerFuture;
        private readonly IInjectionFuture<IObserver<ICloseEvent>> _closeHandlerFuture;
        private int _taskRan = 0;
        private int _taskClosed = 0;

        [Inject]
        private TaskRuntime(
            ITask userTask,
            IDriverMessageHandler driverMessageHandler, 
            IDriverConnectionMessageHandler driverConnectionMessageHandler,
            TaskStatus taskStatus,
            [Parameter(typeof(TaskConfigurationOptions.SuspendHandler))] IInjectionFuture<IObserver<ISuspendEvent>> suspendHandlerFuture,
            [Parameter(typeof(TaskConfigurationOptions.CloseHandler))] IInjectionFuture<IObserver<ICloseEvent>> closedHandlerFuture)
        {
            _currentStatus = taskStatus;
            _driverMessageHandler = Optional<IDriverMessageHandler>.Of(driverMessageHandler);
            _driverConnectionMessageHandler = Optional<IDriverConnectionMessageHandler>.Of(driverConnectionMessageHandler);
            _userTask = userTask;
            _suspendHandlerFuture = suspendHandlerFuture;
            _closeHandlerFuture = closedHandlerFuture;
        }

        public string TaskId
        {
            get { return _currentStatus.TaskId; }
        }

        public string ContextId
        {
            get { return _currentStatus.ContextId; }
        }

        /// <summary>
        /// For testing only!
        /// </summary>
        [Testing]
        internal ITask Task 
        { 
            get { return _userTask; }
        }

        /// <summary>
        /// Runs the task asynchronously.
        /// </summary>
        public Thread StartTaskOnNewThread()
        {
            if (Interlocked.Exchange(ref _taskRan, 1) != 0)
            {
                // Return if we have already called StartTaskOnNewThread
                throw new InvalidOperationException("TaskRun has already been called on TaskRuntime.");
            }

            _currentStatus.SetInit();

            var taskThread = new Thread(() =>
            {
                // The result of the task execution.
                byte[] resultReturnedByTask = null;

                // Whether or not a result shall be returned to the Driver.
                bool returnResultToDriver = true;

                // Exception thrown during `Dispose`, if any.
                Exception exceptionThrownByTaskDispose = null;

                try
                {
                    // Run the handlers for `TaskStart`
                    Logger.Log(Level.Verbose, "Set running status for task");
                    _currentStatus.RunTaskStartHandlers();

                    // Update the state
                    _currentStatus.SetRunning();

                    // Call the Task
                    Logger.Log(Level.Verbose, "Calling into user's task.");
                    resultReturnedByTask = _userTask.Call(null);
                    Logger.Log(Level.Info, "Task Call Finished");

                    if (!_currentStatus.HasEnded())
                    {
                        Logger.Log(Level.Info, "Run the task stop handlers");
                        _currentStatus.RunTaskStopHandlers();
                        // Log the result
                        const Level resultLogLevel = Level.Verbose;
                        if (Logger.IsLoggable(resultLogLevel) && resultReturnedByTask != null &&
                            resultReturnedByTask.Length > 0)
                        {
                            Logger.Log(resultLogLevel,
                                "Task running result:\r\n" +
                                System.Text.Encoding.Default.GetString(resultReturnedByTask));
                        }
                    }
                    else
                    {
                        Logger.Log(Level.Info, "Task not running current state {0}", _currentStatus.State);
                        returnResultToDriver = false;
                    }
                }
                catch (TaskStartHandlerException e)
                {
                    Logger.Log(Level.Info, "TaskRuntime::TaskStartHandlerException");
                    _currentStatus.SetException(e.InnerException);
                    returnResultToDriver = false;
                }
                catch (TaskStopHandlerException e)
                {
                    Logger.Log(Level.Info, "TaskRuntime::TaskStopHandlerException");
                    _currentStatus.SetException(e.InnerException);
                    returnResultToDriver = false;
                }
                catch (Exception e)
                {
                    Logger.Log(Level.Info, "TaskRuntime::Exception {0}", e.GetType());
                    _currentStatus.SetException(e);
                    returnResultToDriver = false;
                }
                finally
                {
                    try
                    {
                        Logger.Log(Level.Info, "Try to dispose of task");
                        _userTask.Dispose();
                        Logger.Log(Level.Info, "task diposed");
                    }
                    catch (Exception e)
                    {
                        exceptionThrownByTaskDispose = new InvalidOperationException("Exception during Task Dispose in task Call()", e);
                    }
                }

                // Inform the driver about the result.
                if (returnResultToDriver)
                {   
                    _currentStatus.SetResult(resultReturnedByTask);
                }

                // If the ITask.Dispose() method threw an Exception, crash the Evaluator.
                if (exceptionThrownByTaskDispose != null)
                {
                    throw exceptionThrownByTaskDispose;
                }               
            });

            taskThread.Start();
            return taskThread;
        }

        public TaskState GetTaskState()
        {
            return _currentStatus.State;
        }

        /// <summary>
        /// Called by heartbeat manager
        /// </summary>
        /// <returns>  current TaskStatusProto </returns>
        public TaskStatusProto GetStatusProto()
        {
            return _currentStatus.ToProto();
        }

        public bool HasEnded()
        {
            return _currentStatus.HasEnded();
        }

        public void Close(byte[] message)
        {
            Logger.Log(Level.Info, "Trying to close Task {0}", TaskId);
            if (Interlocked.Exchange(ref _taskClosed, 1) != 0)
            {
                // Return if we have already called close. This can happen when TaskCloseHandler
                // is invoked and throws an Exception before the Task is completed. The control flows
                // to failing the Evaluator, which eventually tries to close the Task again on Dispose.
                return;
            }

            if (_currentStatus.IsNotRunning())
            {
                Logger.Log(Level.Warning, "Trying to close an task that is in {0} state. Ignored.", _currentStatus.State);
                return;
            }
            try
            {
                OnNext(new CloseEventImpl(message));
                _currentStatus.SetCloseRequested();
            }
            catch (Exception e)
            {
                Logger.Log(Level.Info, "TaskRuntime::TaskClose exception", e);
                _currentStatus.SetException(e);
            }
            finally
            {
                try
                {
                    if (_userTask != null)
                    {
                        _userTask.Dispose();
                    }
                }
                catch (Exception e)
                {
                    var msg = "Exception during Task Dispose in task Close()";
                    Logger.Log(Level.Error, msg);
                    throw new InvalidOperationException(msg, e);
                }
            }
        }

        public void Suspend(byte[] message)
        {
            Logger.Log(Level.Info, string.Format(CultureInfo.InvariantCulture, "Trying to suspend Task {0}", TaskId));

            if (_currentStatus.IsNotRunning())
            {
                Logger.Log(Level.Warning, string.Format(CultureInfo.InvariantCulture, "Trying to suspend an task that is in {0} state. Ignored.", _currentStatus.State));
                return;
            }
            
            // An Exception in suspend should crash the Evaluator.
            try
            {
                OnNext(new SuspendEventImpl(message));
                _currentStatus.SetSuspendRequested();
            }
            catch (Exception e)
            {
                Logger.Log(Level.Info, "TaskRuntime::TaskSuspendHandler exception", e);
                _currentStatus.SetException(e);
                _userTask.Dispose();
            }
        }

        public void Deliver(byte[] message)
        {
            if (_currentStatus.IsNotRunning())
            {
                Logger.Log(Level.Warning, string.Format(CultureInfo.InvariantCulture, "Trying to send a message to an task that is in {0} state. Ignored.", _currentStatus.State));
                return;
            }

            try
            {
                OnNext(new DriverMessageImpl(message));
            }
            catch (Exception e)
            {
                Logger.Log(Level.Error, "TaskRuntime::Deliver message excxeption", e);
                _currentStatus.SetException(e);
                _userTask.Dispose();
            }
        }

        public void OnNext(ICloseEvent value)
        {
            Logger.Log(Level.Info, "TaskRuntime::OnNext(ICloseEvent value)");
            _closeHandlerFuture.Get().OnNext(value);
        }

        public void OnNext(ISuspendEvent value)
        {
            Logger.Log(Level.Info, "TaskRuntime::OnNext(ISuspendEvent value)");
            _suspendHandlerFuture.Get().OnNext(value);
        }

        /// <summary>
        /// Call Handle on the user's DriverMessageHandler.
        /// If the user's handler throws an Exception, the Exception will bubble up as
        /// an Evaluator Exception and fail the Evaluator.
        /// </summary>
        public void OnNext(IDriverMessage value)
        {
            Logger.Log(Level.Verbose, "TaskRuntime::OnNext(IDriverMessage value)");

            if (!_driverMessageHandler.IsPresent())
            {
                return;
            }
            
            _driverMessageHandler.Value.Handle(value);
        }

        /// <summary>
        /// Propagates the IDriverConnection message to the Handler as specified by the Task.
        /// </summary>
        internal void HandleDriverConnectionMessage(IDriverConnectionMessage message)
        {
            if (!_driverConnectionMessageHandler.IsPresent())
            {
                return;
            }

            _driverConnectionMessageHandler.Value.OnNext(message);
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }
    }
}