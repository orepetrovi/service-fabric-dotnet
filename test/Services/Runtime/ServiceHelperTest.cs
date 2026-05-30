// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using System.Fabric.Health;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Runtime;

public abstract class ServiceHelperTest
{
    readonly ServiceHelper sut;

    // Constructor parameters
    readonly string traceType = fuzzy.String();
    readonly string traceId = fuzzy.String();

    readonly IServicePartition partition = Mock.Of<IServicePartition>();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ServiceHelperTest() =>
        sut = new ServiceHelper(traceType, traceId);

    public sealed class AwaitAsyncTaskWithHealthReporting : ServiceHelperTest
    {
        // Method parameters
        readonly Task taskToAwait;
        readonly TimeSpan expectedCancellationTime = TimeSpan.FromMilliseconds(50);
        readonly Action reportHealthFunc = Mock.Of<Action>();

        readonly TaskCompletionSource<int> source = new();

        public AwaitAsyncTaskWithHealthReporting() => taskToAwait = source.Task;

        [Fact]
        public async Task ReturnsWhenTaskToAwaitCompletesWithinExpectedCancellationTime()
        {
            source.SetResult(fuzzy.Int32());

            await sut.AwaitAsyncTaskWithHealthReporting(partition, taskToAwait, expectedCancellationTime, reportHealthFunc);

            Mock.Get(reportHealthFunc).Verify(_ => _(), Times.Never);
        }

        [Fact]
        public async Task PropagatesExceptionWhenTaskToAwaitFaults()
        {
            var expected = new InvalidOperationException(fuzzy.String());
            source.SetException(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.AwaitAsyncTaskWithHealthReporting(partition, taskToAwait, expectedCancellationTime, reportHealthFunc));

            Assert.Same(expected, actual);
            Mock.Get(reportHealthFunc).Verify(_ => _(), Times.Never);
        }

        [Fact]
        public async Task InvokesReportHealthFuncForEachIterationUntilTaskToAwaitCompletes()
        {
            int callCount = 0;
            _ = Mock.Get(reportHealthFunc)
                .Setup(_ => _())
                .Callback(() =>
                {
                    if (Interlocked.Increment(ref callCount) >= 2)
                        source.TrySetResult(fuzzy.Int32());
                });

            await sut.AwaitAsyncTaskWithHealthReporting(partition, taskToAwait, expectedCancellationTime, reportHealthFunc);

            Assert.True(callCount >= 2);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public async Task ThrowsArgumentNullExceptionWhenReportHealthFuncIsNull() =>
            Assert.Equal(nameof(reportHealthFunc), (await Assert.ThrowsAsync<ArgumentNullException>(
                () => sut.AwaitAsyncTaskWithHealthReporting(partition, taskToAwait, expectedCancellationTime, null))).ParamName);
    }

    public sealed class AwaitCloseCommunicationListerWithHealthReporting : ServiceHelperTest
    {
        // Method parameters
        readonly Task closeCommunicationListenerTask;
        readonly string communicationListenerName = fuzzy.String();

        readonly TaskCompletionSource<int> source = new();

        public AwaitCloseCommunicationListerWithHealthReporting() =>
            closeCommunicationListenerTask = source.Task;

        [Fact]
        public async Task ReturnsWhenCloseCommunicationListenerTaskCompletes()
        {
            source.SetResult(fuzzy.Int32());

            await sut.AwaitCloseCommunicationListerWithHealthReporting(partition, closeCommunicationListenerTask, communicationListenerName);

            Mock.Get(partition).Verify(
                _ => _.ReportPartitionHealth(It.IsAny<HealthInformation>()), Times.Never);
        }

        [Fact]
        public async Task PropagatesExceptionWhenCloseCommunicationListenerTaskFaults()
        {
            var expected = new InvalidOperationException(fuzzy.String());
            source.SetException(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.AwaitCloseCommunicationListerWithHealthReporting(partition, closeCommunicationListenerTask, communicationListenerName));

            Assert.Same(expected, actual);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. CommunicationListenerExpectedCloseTimeSpan is a hard-coded 15s constant.
        public void ReportsCommunicationListenerSlowCloseHealthWhenTaskExceedsExpectedCloseTime() =>
            throw new NotImplementedException(
                "ServiceHelper.AwaitCloseCommunicationListerWithHealthReporting hard-codes a 15-second timeout via " +
                "CommunicationListenerExpectedCloseTimeSpan. Triggering the slow-close health report would require a " +
                "15-second wait or a testability seam that the SUT does not expose.");
    }

    public sealed class AwaitRunAsyncWithHealthReporting : ServiceHelperTest
    {
        // Method parameters
        readonly Task runAsyncTask;

        readonly TaskCompletionSource<int> source = new();

        public AwaitRunAsyncWithHealthReporting() => runAsyncTask = source.Task;

        [Fact]
        public async Task ReturnsWhenRunAsyncTaskCompletes()
        {
            source.SetResult(fuzzy.Int32());

            await sut.AwaitRunAsyncWithHealthReporting(partition, runAsyncTask);

            Mock.Get(partition).Verify(
                _ => _.ReportPartitionHealth(It.IsAny<HealthInformation>()), Times.Never);
        }

        [Fact]
        public async Task PropagatesExceptionWhenRunAsyncTaskFaults()
        {
            var expected = new InvalidOperationException(fuzzy.String());
            source.SetException(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.AwaitRunAsyncWithHealthReporting(partition, runAsyncTask));

            Assert.Same(expected, actual);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. RunAsyncExpectedCancellationTimeSpan is a hard-coded 15s constant.
        public void ReportsRunAsyncSlowCancellationHealthWhenTaskExceedsExpectedCancellationTime() =>
            throw new NotImplementedException(
                "ServiceHelper.AwaitRunAsyncWithHealthReporting hard-codes a 15-second timeout via " +
                "RunAsyncExpectedCancellationTimeSpan. Triggering the slow-cancellation health report would require a " +
                "15-second wait or a testability seam that the SUT does not expose.");
    }

    public sealed class HandleRunAsyncUnexpectedException : ServiceHelperTest
    {
        readonly Exception ex = new(fuzzy.String());

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Calls Environment.FailFast which terminates the test process.
        public void ReportsFaultAndCallsFailFast() =>
            throw new NotImplementedException(
                "ServiceHelper.HandleRunAsyncUnexpectedException schedules Environment.FailFast on the thread pool. " +
                "FailFast unconditionally terminates the test host, and the SUT exposes no seam to substitute it, " +
                "so this behavior cannot be covered without testability changes.");

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenPartitionIsNull() =>
            Assert.Equal(nameof(partition), Assert.Throws<ArgumentNullException>(
                () => sut.HandleRunAsyncUnexpectedException(null, ex)).ParamName);
    }

    public sealed class HandleRunAsyncUnexpectedFabricException : ServiceHelperTest
    {
        // Method parameters
        readonly FabricException fex = new(fuzzy.String());

        HealthInformation reported;

        public HandleRunAsyncUnexpectedFabricException() =>
            _ = Mock.Get(partition)
                .Setup(_ => _.ReportPartitionHealth(It.IsAny<HealthInformation>()))
                .Callback<HealthInformation>(hi => reported = hi);

        [Fact]
        public void ReportsRunAsyncUnhandledExceptionHealth()
        {
            sut.HandleRunAsyncUnexpectedFabricException(partition, fex);

            Assert.NotNull(reported);
            Assert.Equal("RunAsync", reported.SourceId);
            Assert.Equal("RunAsyncUnhandledException", reported.Property);
            Assert.Equal(HealthState.Warning, reported.HealthState);
            Assert.Equal(fex.ToString(), reported.Description);
            Assert.Equal(TimeSpan.FromMinutes(2), reported.TimeToLive);
            Assert.True(reported.RemoveWhenExpired);
        }

        [Fact]
        public void ReportsFaultTransient()
        {
            sut.HandleRunAsyncUnexpectedFabricException(partition, fex);

            Mock.Get(partition).Verify(_ => _.ReportFault(FaultType.Transient), Times.Once);
            Mock.Get(partition).Verify(_ => _.ReportFault(It.IsAny<FaultType>()), Times.Once);
        }

        [Fact]
        public void ReportsFaultTransientWhenReportPartitionHealthThrows()
        {
            _ = Mock.Get(partition)
                .Setup(_ => _.ReportPartitionHealth(It.IsAny<HealthInformation>()))
                .Throws(new FabricException(fuzzy.String()));

            sut.HandleRunAsyncUnexpectedFabricException(partition, fex);

            Mock.Get(partition).Verify(_ => _.ReportFault(FaultType.Transient), Times.Once);
        }

        [Fact]
        public void TrimsExceptionDescriptionToMaxHealthDescriptionLength()
        {
            var huge = new HugeException(new string('x', (4 * 1024) + 100));

            sut.HandleRunAsyncUnexpectedFabricException(partition, huge);

            Assert.Equal((4 * 1024) - 1, reported.Description.Length);
            Assert.Equal(huge.ToString().Substring(0, (4 * 1024) - 1), reported.Description);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenPartitionIsNull() =>
            Assert.Equal(nameof(partition), Assert.Throws<ArgumentNullException>(
                () => sut.HandleRunAsyncUnexpectedFabricException(null, fex)).ParamName);

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenFexIsNull() =>
            Assert.Equal(nameof(fex), Assert.Throws<ArgumentNullException>(
                () => sut.HandleRunAsyncUnexpectedFabricException(partition, null)).ParamName);

        sealed class HugeException : FabricException
        {
            readonly string text;
            public HugeException(string text) => this.text = text;
            public override string ToString() => text;
        }
    }

    public sealed class ObserveExceptionIfAny : ServiceHelperTest
    {
        // Method parameters
        readonly Task tsk;

        readonly TaskCompletionSource<int> source = new();

        public ObserveExceptionIfAny() => tsk = source.Task;

        [Fact]
        public void DoesNotThrowWhenTaskCompletesSuccessfully()
        {
            source.SetResult(fuzzy.Int32());
            ServiceHelper.ObserveExceptionIfAny(tsk);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Only observable effect is suppressing TaskScheduler.UnobservedTaskException.
        public void ObservesFaultedTaskException() =>
            throw new NotImplementedException(
                "ServiceHelper.ObserveExceptionIfAny awaits the supplied task on a fire-and-forget Task.Run to mark " +
                "its exception observed. The only observable effect is preventing TaskScheduler.UnobservedTaskException " +
                "from firing on finalization, which can only be asserted via GC.Collect + WaitForPendingFinalizers and " +
                "a TaskScheduler.UnobservedTaskException handler — a known-fragile pattern. The SUT exposes no seam to " +
                "verify the suppression directly.");
    }
}
