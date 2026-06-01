// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Inspector;
using Microsoft.ServiceFabric.Services.Communication;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Runtime;

public abstract class StatelessServiceInstanceAdapterTest
{
    readonly IStatelessServiceInstance sut;

    // Constructor parameters
    readonly StatelessServiceContext context = fuzzy.StatelessServiceContext();
    readonly Mock<IStatelessUserServiceInstance> userServiceInstance = new() { DefaultValue = DefaultValue.Mock };

    // Test fixture
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    private StatelessServiceInstanceAdapterTest() =>
        sut = new StatelessServiceInstanceAdapter(context, userServiceInstance.Object);

    public sealed class Abort : StatelessServiceInstanceAdapterTest
    {
        [Fact]
        public void AbortsCommunicationListeners()
        {
            var listener = new Mock<ICommunicationListener>();
            sut.Field<IList<CommunicationListenerInfo>>().Set([new(fuzzy.String(), listener.Object)]);

            sut.Abort();

            listener.Verify(_ => _.Abort(), Times.Once);
            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
        }

        [Fact]
        public void SwallowsExceptionsThrownByCommunicationListenerAbort()
        {
            var throwing = new Mock<ICommunicationListener>();
            _ = throwing.Setup(_ => _.Abort()).Throws(new InvalidOperationException(fuzzy.String()));
            var following = new Mock<ICommunicationListener>();
            sut.Field<IList<CommunicationListenerInfo>>().Set(
            [
                new(fuzzy.String(), throwing.Object),
                new(fuzzy.String(), following.Object),
            ]);

            sut.Abort();

            throwing.Verify(_ => _.Abort(), Times.Once);
            following.Verify(_ => _.Abort(), Times.Once);
            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
        }

        [Fact]
        public void InvokesUserServiceOnAbort()
        {
            sut.Abort();
            userServiceInstance.Verify(_ => _.OnAbort(), Times.Once);
        }

        [Fact]
        public void CancelsRunAsyncCancellationTokenSource()
        {
            var existingCts = new CancellationTokenSource();
            sut.Field<CancellationTokenSource>().Set(existingCts);
            sut.Field<Task>().Set(Task.CompletedTask);

            sut.Abort();

            Assert.True(existingCts.IsCancellationRequested);
        }

        [Fact]
        public void DoesNothingToCancellationTokenSourceWhenItIsNull() =>
            sut.Abort();
    }

    public sealed class CloseAsync : StatelessServiceInstanceAdapterTest
    {
        // Method parameters
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public async Task ClosesCommunicationListeners()
        {
            var listener = new Mock<ICommunicationListener>();
            sut.Field<IList<CommunicationListenerInfo>>().Set([new(fuzzy.String(), listener.Object)]);

            await sut.CloseAsync(cancellationToken);

            listener.Verify(_ => _.CloseAsync(cancellationToken), Times.Once);
            listener.Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            listener.Verify(_ => _.Abort(), Times.Never);
            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
        }

        [Fact]
        public async Task AbortsCommunicationListenersWhenCloseAsyncThrows()
        {
            var listener = new Mock<ICommunicationListener>();
            _ = listener
                .Setup(_ => _.CloseAsync(cancellationToken))
                .ThrowsAsync(new InvalidOperationException(fuzzy.String()));
            sut.Field<IList<CommunicationListenerInfo>>().Set([new(fuzzy.String(), listener.Object)]);

            await sut.CloseAsync(cancellationToken);

            listener.Verify(_ => _.Abort(), Times.Once);
            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
        }

        [Fact]
        public async Task InvokesUserServiceOnCloseAsync()
        {
            await sut.CloseAsync(cancellationToken);
            userServiceInstance.Verify(_ => _.OnCloseAsync(cancellationToken), Times.Once);
            userServiceInstance.Verify(_ => _.OnCloseAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelsAndClearsRunAsyncTaskAndCancellationTokenSource()
        {
            var existingCts = new CancellationTokenSource();
            sut.Field<CancellationTokenSource>().Set(existingCts);
            sut.Field<Task>().Set(Task.CompletedTask);

            await sut.CloseAsync(cancellationToken);

            Assert.True(existingCts.IsCancellationRequested);
            Assert.Null(sut.Field<CancellationTokenSource>().Value);
            Assert.Null(sut.Field<Task>().Value);
        }

        [Fact]
        public async Task RethrowsOperationCanceledExceptionFromRunAsyncTaskWhenTokenDoesNotMatch()
        {
            var expected = new OperationCanceledException(new CancellationToken(canceled: true));
            sut.Field<CancellationTokenSource>().Set(new CancellationTokenSource());
            sut.Field<Task>().Set(Task.FromException(expected));

            var actual = await Assert.ThrowsAsync<OperationCanceledException>(
                () => sut.CloseAsync(cancellationToken));

            Assert.Same(expected, actual);
            Assert.Null(sut.Field<CancellationTokenSource>().Value);
            Assert.Null(sut.Field<Task>().Value);
        }

        [Fact]
        public async Task RethrowsUnexpectedExceptionFromRunAsyncTask()
        {
            var expected = new InvalidOperationException(fuzzy.String());
            sut.Field<CancellationTokenSource>().Set(new CancellationTokenSource());
            sut.Field<Task>().Set(Task.FromException(expected));

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.CloseAsync(cancellationToken));

            Assert.Same(expected, actual);
            Assert.Null(sut.Field<CancellationTokenSource>().Value);
            Assert.Null(sut.Field<Task>().Value);
        }
    }

    public sealed class Constructor : StatelessServiceInstanceAdapterTest
    {
        [Fact]
        public void ThrowsArgumentNullExceptionWhenContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new StatelessServiceInstanceAdapter(null, userServiceInstance.Object));
            Assert.Equal(nameof(context), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenUserServiceInstanceIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new StatelessServiceInstanceAdapter(context, null));
            Assert.Equal(nameof(userServiceInstance), exception.ParamName);
        }

        [Fact]
        public void SetsUserServiceInstanceAddressesToEmptyReadOnlyDictionary()
        {
            userServiceInstance.VerifySet(
                _ => _.Addresses = It.Is<IReadOnlyDictionary<string, string>>(d => d.Count == 0),
                Times.Once());
            userServiceInstance.VerifySet(_ => _.Addresses = It.IsAny<IReadOnlyDictionary<string, string>>(), Times.Once());
        }
    }

    public sealed class OpenAsync : StatelessServiceInstanceAdapterTest
    {
        // Method parameters
        readonly Mock<IStatelessServicePartition> partition = new();
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public async Task SetsServicePartition()
        {
            _ = await sut.OpenAsync(partition.Object, cancellationToken);
            Assert.Same(partition.Object, sut.Field<IStatelessServicePartition>().Value);
        }

        [Fact]
        public async Task SetsUserServiceInstancePartition()
        {
            _ = await sut.OpenAsync(partition.Object, cancellationToken);
            userServiceInstance.VerifySet(_ => _.Partition = partition.Object, Times.Once());
            userServiceInstance.VerifySet(_ => _.Partition = It.IsAny<IStatelessServicePartition>(), Times.Once());
        }

        [Fact]
        public async Task OpensCommunicationListenersAndReturnsTheirEndpoints()
        {
            string name1 = fuzzy.String();
            string name2 = name1 + fuzzy.String();
            string address1 = fuzzy.String();
            string address2 = address1 + fuzzy.String();
            var listener1 = new Mock<ICommunicationListener>();
            var listener2 = new Mock<ICommunicationListener>();
            _ = listener1.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(address1);
            _ = listener2.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(address2);
            _ = userServiceInstance.Setup(_ => _.CreateServiceInstanceListeners()).Returns(
            [
                new ServiceInstanceListener(_ => listener1.Object, name1),
                new ServiceInstanceListener(_ => listener2.Object, name2),
            ]);

            string actual = await sut.OpenAsync(partition.Object, cancellationToken);
            await sut.Field<Task>().Value;

            listener1.Verify(_ => _.OpenAsync(cancellationToken), Times.Once);
            listener1.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
            listener2.Verify(_ => _.OpenAsync(cancellationToken), Times.Once);
            listener2.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
            userServiceInstance.Verify(_ => _.CreateServiceInstanceListeners(), Times.Once);

            var expected = new ServiceEndpointCollection();
            expected.AddEndpoint(name1, address1);
            expected.AddEndpoint(name2, address2);
            Assert.Equal(expected.ToString(), actual);
        }

        [Fact]
        public async Task InvokesCreateServiceInstanceListeners()
        {
            _ = await sut.OpenAsync(partition.Object, cancellationToken);
            userServiceInstance.Verify(_ => _.CreateServiceInstanceListeners(), Times.Once);
        }

        [Fact]
        public async Task InvokesUserServiceOnOpenAsync()
        {
            _ = await sut.OpenAsync(partition.Object, cancellationToken);
            userServiceInstance.Verify(_ => _.OnOpenAsync(cancellationToken), Times.Once);
            userServiceInstance.Verify(_ => _.OnOpenAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdatesUserServiceInstanceAddressesFromOpenedListeners()
        {
            string name = fuzzy.String();
            string address = fuzzy.String();
            var listener = new Mock<ICommunicationListener>();
            _ = listener.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(address);
            _ = userServiceInstance.Setup(_ => _.CreateServiceInstanceListeners())
                .Returns([new ServiceInstanceListener(_ => listener.Object, name)]);

            _ = await sut.OpenAsync(partition.Object, cancellationToken);
            await sut.Field<Task>().Value;

            userServiceInstance.VerifySet(
                _ => _.Addresses = It.Is<IReadOnlyDictionary<string, string>>(d => d.Count == 0),
                Times.Once());
            userServiceInstance.VerifySet(
                _ => _.Addresses = It.Is<IReadOnlyDictionary<string, string>>(d => d.ContainsKey(name) && d[name] == address),
                Times.Once());
            userServiceInstance.VerifySet(_ => _.Addresses = It.IsAny<IReadOnlyDictionary<string, string>>(), Times.Exactly(2));
            userServiceInstance.Verify(_ => _.CreateServiceInstanceListeners(), Times.Once);
        }

        [Fact]
        public async Task SkipsNullInstanceListeners()
        {
            _ = userServiceInstance.Setup(_ => _.CreateServiceInstanceListeners()).Returns([null]);

            _ = await sut.OpenAsync(partition.Object, cancellationToken);

            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
            userServiceInstance.Verify(_ => _.CreateServiceInstanceListeners(), Times.Once);
        }

        [Fact]
        public async Task SkipsListenersWhenCreateCommunicationListenerReturnsNull()
        {
            _ = userServiceInstance.Setup(_ => _.CreateServiceInstanceListeners())
                .Returns([fuzzy.ServiceInstanceListener()]);
            sut.Field<Func<ServiceInstanceListener, StatelessServiceContext, CommunicationListenerInfo>>()
                .Set((_, _) => null);

            _ = await sut.OpenAsync(partition.Object, cancellationToken);

            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
            userServiceInstance.Verify(_ => _.CreateServiceInstanceListeners(), Times.Once);
        }

        [Fact]
        public async Task AbortsCommunicationListenersAndRethrowsWhenListenerOpenAsyncThrows()
        {
            var expected = new InvalidOperationException(fuzzy.String());
            var listener = new Mock<ICommunicationListener>();
            _ = listener.Setup(_ => _.OpenAsync(cancellationToken)).ThrowsAsync(expected);
            _ = userServiceInstance.Setup(_ => _.CreateServiceInstanceListeners())
                .Returns([new ServiceInstanceListener(_ => listener.Object, fuzzy.String())]);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.OpenAsync(partition.Object, cancellationToken));

            Assert.Same(expected, actual);
            listener.Verify(_ => _.Abort(), Times.Once);
            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
            userServiceInstance.Verify(_ => _.CreateServiceInstanceListeners(), Times.Once);
        }

        [Fact]
        public async Task ClosesCommunicationListenersAndRethrowsWhenOnOpenAsyncThrows()
        {
            var expected = new InvalidOperationException(fuzzy.String());
            _ = userServiceInstance.Setup(_ => _.OnOpenAsync(cancellationToken)).ThrowsAsync(expected);
            var listener = new Mock<ICommunicationListener>();
            _ = listener.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(fuzzy.String());
            _ = userServiceInstance.Setup(_ => _.CreateServiceInstanceListeners())
                .Returns([new ServiceInstanceListener(_ => listener.Object, fuzzy.String())]);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.OpenAsync(partition.Object, cancellationToken));

            Assert.Same(expected, actual);
            listener.Verify(_ => _.CloseAsync(cancellationToken), Times.Once);
            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
            userServiceInstance.Verify(_ => _.CreateServiceInstanceListeners(), Times.Once);
        }

        [Fact]
        public async Task SchedulesExecuteRunAsyncTask()
        {
            _ = await sut.OpenAsync(partition.Object, cancellationToken);

            var task = sut.Field<Task>().Value;
            var cts = sut.Field<CancellationTokenSource>().Value;
            Assert.NotNull(task);
            Assert.NotNull(cts);
            Assert.False(cts.IsCancellationRequested);

            await task;
        }

        [Fact]
        public async Task InvokesUserServiceRunAsync()
        {
            _ = await sut.OpenAsync(partition.Object, cancellationToken);
            CancellationToken runAsyncToken = sut.Field<CancellationTokenSource>().Value.Token;
            await sut.Field<Task>().Value;

            userServiceInstance.Verify(_ => _.RunAsync(runAsyncToken), Times.Once);
            partition.Verify(_ => _.ReportFault(It.IsAny<FaultType>()), Times.Never);
        }

        [Fact]
        public async Task SwallowsOperationCanceledExceptionWhenTokenMatchesRunAsyncCancellation()
        {
            var started = new TaskCompletionSource<bool>();
            _ = userServiceInstance
                .Setup(_ => _.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(async ct =>
                {
                    _ = started.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct);
                });

            _ = await sut.OpenAsync(partition.Object, cancellationToken);
            _ = await started.Task;
            sut.Field<CancellationTokenSource>().Value.Cancel();

            await sut.Field<Task>().Value;

            partition.Verify(_ => _.ReportFault(It.IsAny<FaultType>()), Times.Never);
        }

        [Fact]
        public async Task ReportsTransientFaultWhenRunAsyncThrowsFabricException()
        {
            _ = userServiceInstance
                .Setup(_ => _.RunAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new FabricException(fuzzy.String()));

            _ = await sut.OpenAsync(partition.Object, cancellationToken);
            await sut.Field<Task>().Value;

            partition.Verify(_ => _.ReportFault(FaultType.Transient), Times.Once);
            partition.Verify(_ => _.ReportFault(It.IsAny<FaultType>()), Times.Once);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Environment.FailFast
        public void FailsFastWhenRunAsyncThrowsNonMatchingOperationCanceledException() =>
            // ExecuteRunAsync routes an OperationCanceledException whose token does not match
            // runAsynCancellationTokenSource.Token through ServiceHelper.HandleRunAsyncUnexpectedException,
            // which calls Environment.FailFast and terminates the test host before any assertion can run.
            throw new NotImplementedException();

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Environment.FailFast
        public void FailsFastWhenRunAsyncThrowsUnexpectedException() =>
            // ExecuteRunAsync routes non-FabricException exceptions through
            // ServiceHelper.HandleRunAsyncUnexpectedException, which calls Environment.FailFast
            // and terminates the test host before any assertion can run.
            throw new NotImplementedException();

        [Fact(Explicit = true)] // TODO: SUT testability limitation. RunAsyncExpectedCancellationTimeSpan is hard-coded.
        public void ReportsSlowCancellationHealthWhenRunAsyncCancellationExceedsExpectedTime() =>
            // CancelRunAsync compares the elapsed cancellation time against the hard-coded
            // ServiceHelper.RunAsyncExpectedCancellationTimeSpan (15s). Without a way to inject a shorter
            // timeout, exercising the slow-cancellation branch in a unit test is impractical.
            throw new NotImplementedException();
    }

    public sealed class Test_IsRunAsyncTaskRunning : StatelessServiceInstanceAdapterTest
    {
        new readonly StatelessServiceInstanceAdapter sut;

        public Test_IsRunAsyncTaskRunning() => sut = (StatelessServiceInstanceAdapter)base.sut;

        [Fact]
        public void ReturnsTrueWhenExecuteRunAsyncTaskIsNotCompleted()
        {
            base.sut.Field<Task>().Set(new TaskCompletionSource<bool>().Task);
            Assert.True(sut.Test_IsRunAsyncTaskRunning());
        }

        [Fact]
        public void ReturnsFalseWhenExecuteRunAsyncTaskIsCompleted()
        {
            base.sut.Field<Task>().Set(Task.CompletedTask);
            Assert.False(sut.Test_IsRunAsyncTaskRunning());
        }

        [Fact]
        public void ReturnsFalseWhenExecuteRunAsyncTaskIsCanceled()
        {
            base.sut.Field<Task>().Set(Task.FromCanceled(new CancellationToken(true)));
            Assert.False(sut.Test_IsRunAsyncTaskRunning());
        }

        [Fact]
        public void ReturnsFalseWhenExecuteRunAsyncTaskIsFaulted()
        {
            base.sut.Field<Task>().Set(Task.FromException(new InvalidOperationException()));
            Assert.False(sut.Test_IsRunAsyncTaskRunning());
        }
    }
}
