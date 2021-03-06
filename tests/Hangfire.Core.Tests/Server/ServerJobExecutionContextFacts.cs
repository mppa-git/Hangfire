﻿using System;
using System.Collections.Generic;
using System.Threading;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using Moq;
using Xunit;

namespace Hangfire.Core.Tests.Server
{
    public class ServerJobExecutionContextFacts
    {
        private const string JobId = "my-job";
        private readonly Mock<IStorageConnection> _connection;
        private CancellationToken _shutdownToken;
        private readonly WorkerContextMock _workerContextMock;
        private readonly StateData _stateData;

        public ServerJobExecutionContextFacts()
        {
            _stateData = new StateData
            {
                Name = ProcessingState.StateName,
                Data = new Dictionary<string, string>
                {
                    { "WorkerNumber", "1" },
                    { "ServerId", "Server" },
                }
            };

            _connection = new Mock<IStorageConnection>();
            _connection.Setup(x => x.GetStateData(JobId)).Returns(_stateData);

            _workerContextMock = new WorkerContextMock { WorkerNumber = 1 };
            _workerContextMock.ServerId = "Server";

            _shutdownToken = new CancellationToken(false);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenJobIsIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ServerJobExecutionContext(
                    null, _connection.Object, _workerContextMock.Object, new CancellationToken()));

            Assert.Equal("jobId", exception.ParamName);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenConnectionIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ServerJobExecutionContext(
                    JobId, null, _workerContextMock.Object, new CancellationToken()));

            Assert.Equal("connection", exception.ParamName);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenWorkerContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ServerJobExecutionContext(
                    JobId, _connection.Object, null, new CancellationToken()));

            Assert.Equal("workerContext", exception.ParamName);
        }

        [Fact]
        public void ShutdownTokenProperty_PointsToShutdownTokenValue()
        {
            var token = CreateToken();
            Assert.Equal(_shutdownToken, token.ShutdownToken);
        }

        [Fact]
        public void JobId_ReturnsJobId()
        {
            var context = CreateContext();

            Assert.Equal(JobId, context.JobId);
        }

        [Fact]
        public void ThrowIfCancellationRequested_DoesNotThrowOnProcessingJob_IfNoShutdownRequested()
        {
            var token = CreateToken();

            Assert.DoesNotThrow(token.ThrowIfCancellationRequested);
        }

        [Fact]
        public void ThrowIfCancellationRequested_ThrowsOperationCanceled_OnShutdownRequest()
        {
            _shutdownToken = new CancellationToken(true);
            var token = CreateToken();

            Assert.Throws<OperationCanceledException>(
                () => token.ThrowIfCancellationRequested());
        }

        [Fact]
        public void ThrowIfCancellationRequested_Throws_IfStateDataDoesNotExist()
        {
            _connection.Setup(x => x.GetStateData(It.IsAny<string>())).Returns((StateData)null);
            var token = CreateToken();

            Assert.Throws<JobAbortedException>(() => token.ThrowIfCancellationRequested());
        }

        [Fact]
        public void ThrowIfCancellationRequested_ThrowsJobAborted_IfJobIsNotInProcessingState()
        {
            _stateData.Name = "NotProcessing";
            var token = CreateToken();

            Assert.Throws<JobAbortedException>(
                () => token.ThrowIfCancellationRequested());
        }

        [Fact]
        public void ThrowIfCancellationRequested_ThrowsJobAborted_IfStateData_ContainsDifferentServerId()
        {
            _stateData.Data["ServerId"] = "AnotherServer";
            var token = CreateToken();

            Assert.Throws<JobAbortedException>(
                () => token.ThrowIfCancellationRequested());
        }

        [Fact]
        public void ThrowIfCancellationRequested_ThrowsJobAborted_IfWorkerNumberWasChanged()
        {
            _stateData.Data["WorkerNumber"] = "999";
            var token = CreateToken();

            Assert.Throws<JobAbortedException>(
                () => token.ThrowIfCancellationRequested());
        }

        private IJobCancellationToken CreateToken()
        {
            return this.CreateContext();
        }

        private IJobExecutionContext CreateContext()
        {
            return new ServerJobExecutionContext(
                JobId, _connection.Object, _workerContextMock.Object, _shutdownToken);
        }
    }
}
