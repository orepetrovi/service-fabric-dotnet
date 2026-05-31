// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Inspector;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Communication;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Runtime;

public abstract class StatefulServiceReplicaAdapterTest
{
    readonly IStatefulServiceReplica sut;

    // Constructor parameters
    readonly StatefulServiceContext context = fuzzy.StatefulServiceContext();
    readonly Mock<IStatefulUserServiceReplica> userServiceReplica = new() { DefaultValue = DefaultValue.Mock };
    readonly Mock<IStateProviderReplica> stateProvider = new() { DefaultValue = DefaultValue.Mock };

    // Test fixture
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    StatefulServiceReplicaAdapterTest()
    {
        _ = userServiceReplica.Setup(_ => _.CreateStateProviderReplica()).Returns(stateProvider.Object);
        sut = new StatefulServiceReplicaAdapter(context, userServiceReplica.Object);
    }

    public sealed class Abort : StatefulServiceReplicaAdapterTest
    {
        [Fact]
        public void AbortsCommunicationListeners()
        {
            var listener = new Mock<ICommunicationListener>();
            sut.Field<IList<CommunicationListenerInfo>>().Set(new List<CommunicationListenerInfo>
            {
                new(fuzzy.String(), listener.Object),
            });

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
            sut.Field<IList<CommunicationListenerInfo>>().Set(new List<CommunicationListenerInfo>
            {
                new(fuzzy.String(), throwing.Object),
                new(fuzzy.String(), following.Object),
            });

            sut.Abort();

            throwing.Verify(_ => _.Abort(), Times.Once);
            following.Verify(_ => _.Abort(), Times.Once);
            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
        }

        [Fact]
        public void InvokesUserServiceOnAbort()
        {
            sut.Abort();
            userServiceReplica.Verify(_ => _.OnAbort(), Times.Once);
        }

        [Fact]
        public void AbortsStateProviderReplicaAndClearsIt()
        {
            sut.Abort();

            stateProvider.Verify(_ => _.Abort(), Times.Once);
            Assert.Null(sut.Field<IStateProviderReplica>().Value);
        }

        [Fact]
        public void DoesNothingToStateProviderReplicaWhenItIsNull()
        {
            sut.Field<IStateProviderReplica>().Set(null);
            sut.Abort();
            Assert.Null(sut.Field<IStateProviderReplica>().Value);
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
    }

    public sealed class ChangeRoleAsync : StatefulServiceReplicaAdapterTest
    {
        // Method parameters
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Primary transitions schedule a background task that reads servicePartition.WriteStatus
        // and reports faults via servicePartition.ReportFault. Provide a partition with Granted
        // write status so the background task completes deterministically without an NRE.
        readonly Mock<IStatefulServicePartition> partition = new();

        public ChangeRoleAsync()
        {
            _ = partition.SetupGet(_ => _.WriteStatus).Returns(PartitionAccessStatus.Granted);
            sut.Field<IStatefulServicePartition>().Set(partition.Object);
        }

        [Fact]
        public async Task ClosesExistingCommunicationListenersBeforeOpeningNew()
        {
            var existing = new Mock<ICommunicationListener>();
            sut.Field<IList<CommunicationListenerInfo>>().Set(new List<CommunicationListenerInfo>
            {
                new(fuzzy.String(), existing.Object),
            });

            _ = await sut.ChangeRoleAsync(ReplicaRole.None, cancellationToken);

            existing.Verify(_ => _.CloseAsync(cancellationToken), Times.Once);
            existing.Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            existing.Verify(_ => _.Abort(), Times.Never);
            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
        }

        [Fact]
        public async Task ContinuesChangeRoleFlowAfterAbortingListenersWhenCloseAsyncThrows()
        {
            var existing = new Mock<ICommunicationListener>();
            _ = existing
                .Setup(_ => _.CloseAsync(cancellationToken))
                .ThrowsAsync(new InvalidOperationException(fuzzy.String()));
            sut.Field<IList<CommunicationListenerInfo>>().Set(new List<CommunicationListenerInfo>
            {
                new(fuzzy.String(), existing.Object),
            });

            _ = await sut.ChangeRoleAsync(ReplicaRole.None, cancellationToken);

            existing.Verify(_ => _.Abort(), Times.Once);
            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
            stateProvider.Verify(_ => _.ChangeRoleAsync(ReplicaRole.None, cancellationToken), Times.Once);
            userServiceReplica.Verify(_ => _.OnChangeRoleAsync(ReplicaRole.None, cancellationToken), Times.Once);
        }

        [Theory]
        [InlineData(ReplicaRole.Primary)]
        [InlineData(ReplicaRole.IdleSecondary)]
        [InlineData(ReplicaRole.ActiveSecondary)]
        [InlineData(ReplicaRole.None)]
        public async Task ForwardsToStateProviderReplica(ReplicaRole newRole)
        {
            _ = await sut.ChangeRoleAsync(newRole, cancellationToken);
            stateProvider.Verify(_ => _.ChangeRoleAsync(newRole, cancellationToken), Times.Once);
            stateProvider.Verify(_ => _.ChangeRoleAsync(It.IsAny<ReplicaRole>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvokesUserServiceOnChangeRoleAsyncAfterStateProvider()
        {
            int order = 0;
            int stateProviderOrder = 0;
            int userOrder = 0;
            _ = stateProvider
                .Setup(_ => _.ChangeRoleAsync(ReplicaRole.None, cancellationToken))
                .Callback(() => stateProviderOrder = ++order)
                .Returns(Task.CompletedTask);
            _ = userServiceReplica
                .Setup(_ => _.OnChangeRoleAsync(ReplicaRole.None, cancellationToken))
                .Callback(() => userOrder = ++order)
                .Returns(Task.CompletedTask);

            _ = await sut.ChangeRoleAsync(ReplicaRole.None, cancellationToken);

            Assert.Equal(1, stateProviderOrder);
            Assert.Equal(2, userOrder);
        }

        [Fact]
        public async Task ReturnsEmptyStringWhenNoListenersAreOpen()
        {
            string actual = await sut.ChangeRoleAsync(ReplicaRole.None, cancellationToken);
            Assert.Equal(string.Empty, actual);
        }

        [Fact]
        public async Task OpensCommunicationListenersAndReturnsTheirEndpointsWhenNewRoleIsPrimary()
        {
            string name1 = fuzzy.String();
            string name2 = name1 + fuzzy.String();
            string address1 = fuzzy.String();
            string address2 = address1 + fuzzy.String();
            var listener1 = new Mock<ICommunicationListener>();
            var listener2 = new Mock<ICommunicationListener>();
            _ = listener1.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(address1);
            _ = listener2.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(address2);
            _ = userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners()).Returns(new[]
            {
                new ServiceReplicaListener(_ => listener1.Object, name1),
                new ServiceReplicaListener(_ => listener2.Object, name2),
            });

            string actual = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

            listener1.Verify(_ => _.OpenAsync(cancellationToken), Times.Once);
            listener1.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
            listener2.Verify(_ => _.OpenAsync(cancellationToken), Times.Once);
            listener2.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);

            var expected = new ServiceEndpointCollection();
            expected.AddEndpoint(name1, address1);
            expected.AddEndpoint(name2, address2);
            Assert.Equal(expected.ToString(), actual);
        }

        [Fact]
        public async Task StoresOpenedCommunicationListenersWhenNewRoleIsPrimary()
        {
            string name = fuzzy.String();
            var listener = new Mock<ICommunicationListener>();
            _ = listener.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(fuzzy.String());
            _ = userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners())
                .Returns(new[] { new ServiceReplicaListener(_ => listener.Object, name) });
            sut.Field<Func<ServiceReplicaListener, StatefulServiceContext, CommunicationListenerInfo>>()
                .Set((entry, _) => new CommunicationListenerInfo(entry.Name, listener.Object));

            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

            CommunicationListenerInfo stored = Assert.Single(sut.Field<IList<CommunicationListenerInfo>>().Value);
            Assert.Equal(name, stored.Name);
            Assert.Same(listener.Object, stored.Listener);
        }

        [Fact]
        public async Task UpdatesUserServiceReplicaAddressesFromOpenedListenersWhenNewRoleIsPrimary()
        {
            string name = fuzzy.String();
            string address = fuzzy.String();
            var listener = new Mock<ICommunicationListener>();
            _ = listener.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(address);

            _ = userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners())
                .Returns(new[] { new ServiceReplicaListener(_ => listener.Object, name) });

            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

            userServiceReplica.VerifySet(
                _ => _.Addresses = It.Is<IReadOnlyDictionary<string, string>>(d => d.Count == 0),
                Times.Once());
            userServiceReplica.VerifySet(
                _ => _.Addresses = It.Is<IReadOnlyDictionary<string, string>>(d => d.ContainsKey(name) && d[name] == address),
                Times.Once());
            userServiceReplica.VerifySet(_ => _.Addresses = It.IsAny<IReadOnlyDictionary<string, string>>(), Times.Exactly(2));
        }

        [Fact]
        public async Task SchedulesExecuteRunAsyncTaskWhenNewRoleIsPrimary()
        {
            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

            var task = sut.Field<Task>().Value;
            var cts = sut.Field<CancellationTokenSource>().Value;
            Assert.NotNull(task);
            Assert.NotNull(cts);
            Assert.False(cts.IsCancellationRequested);

            await task;
            Assert.Same(task, sut.Field<Task>().Value);
        }

        [Fact]
        public async Task InvokesUserServiceRunAsyncWhenWriteStatusIsGranted()
        {
            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);
            CancellationToken runAsyncToken = sut.Field<CancellationTokenSource>().Value.Token;
            await sut.Field<Task>().Value;

            userServiceReplica.Verify(_ => _.RunAsync(runAsyncToken), Times.Once);
            userServiceReplica.Verify(_ => _.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
            partition.Verify(_ => _.ReportFault(It.IsAny<FaultType>()), Times.Never);
        }

        [Fact]
        public async Task DoesNotInvokeUserServiceRunAsyncWhenWriteStatusIsNotPrimary()
        {
            _ = partition.SetupGet(_ => _.WriteStatus).Returns(PartitionAccessStatus.NotPrimary);

            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);
            await sut.Field<Task>().Value;

            userServiceReplica.Verify(_ => _.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
            partition.Verify(_ => _.ReportFault(It.IsAny<FaultType>()), Times.Never);
        }

        [Fact]
        public async Task DoesNotInvokeUserServiceRunAsyncWhenWriteStatusThrowsFabricObjectClosedException()
        {
            _ = partition.SetupGet(_ => _.WriteStatus).Throws(new FabricObjectClosedException());

            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);
            await sut.Field<Task>().Value;

            userServiceReplica.Verify(_ => _.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
            partition.Verify(_ => _.ReportFault(It.IsAny<FaultType>()), Times.Never);
        }

        [Fact]
        public async Task ReportsTransientFaultWhenWriteStatusThrowsUnexpectedException()
        {
            _ = partition.SetupGet(_ => _.WriteStatus).Throws(new InvalidOperationException(fuzzy.String()));

            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);
            await sut.Field<Task>().Value;

            userServiceReplica.Verify(_ => _.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
            partition.Verify(_ => _.ReportFault(FaultType.Transient), Times.Once);
            partition.Verify(_ => _.ReportFault(It.IsAny<FaultType>()), Times.Once);
        }

        [Fact]
        public async Task RetriesAcquiringWriteStatusWhenReconfigurationPending()
        {
            _ = partition.SetupSequence(_ => _.WriteStatus)
                .Returns(PartitionAccessStatus.ReconfigurationPending)
                .Returns(PartitionAccessStatus.Granted);

            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);
            await sut.Field<Task>().Value;

            userServiceReplica.Verify(_ => _.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
            partition.VerifyGet(_ => _.WriteStatus, Times.Exactly(2));
        }

        [Fact]
        public async Task SwallowsOperationCanceledExceptionWhenTokenMatchesRunAsyncCancellation()
        {
            // User's RunAsync blocks until its cancellation token fires, then throws a matching OCE.
            // The adapter must observe the token cancellation and complete the executeRunAsyncTask
            // without rethrowing or reporting a fault.
            var started = new TaskCompletionSource<bool>();
            _ = userServiceReplica
                .Setup(_ => _.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(async ct =>
                {
                    _ = started.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct);
                });

            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);
            _ = await started.Task;
            sut.Field<CancellationTokenSource>().Value.Cancel();

            await sut.Field<Task>().Value;

            partition.Verify(_ => _.ReportFault(It.IsAny<FaultType>()), Times.Never);
        }

        [Fact]
        public async Task ReportsTransientFaultWhenRunAsyncThrowsFabricException()
        {
            _ = userServiceReplica
                .Setup(_ => _.RunAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new FabricException(fuzzy.String()));

            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);
            await sut.Field<Task>().Value;

            partition.Verify(_ => _.ReportFault(FaultType.Transient), Times.Once);
            partition.Verify(_ => _.ReportFault(It.IsAny<FaultType>()), Times.Once);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Environment.FailFast
        public void FailsFastWhenRunAsyncThrowsNonMatchingOperationCanceledException() =>
            // ExecuteRunAsync routes an OperationCanceledException whose token does not match
            // runAsyncCancellationTokenSource.Token through ServiceHelper.HandleRunAsyncUnexpectedException,
            // which calls Environment.FailFast and terminates the test host before any assertion can run.
            throw new NotImplementedException();

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Environment.FailFast
        public void FailsFastWhenRunAsyncThrowsUnexpectedException() =>
            // ExecuteRunAsync routes non-FabricException exceptions through
            // ServiceHelper.HandleRunAsyncUnexpectedException, which calls Environment.FailFast
            // and terminates the test host before any assertion can run.
            throw new NotImplementedException();

        [Fact]
        public async Task CancelsAndClearsRunAsyncTaskAndCancellationTokenSourceWhenNewRoleIsNotPrimary()
        {
            var existingCts = new CancellationTokenSource();
            sut.Field<CancellationTokenSource>().Set(existingCts);
            sut.Field<Task>().Set(Task.CompletedTask);

            _ = await sut.ChangeRoleAsync(ReplicaRole.ActiveSecondary, cancellationToken);

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
                () => sut.ChangeRoleAsync(ReplicaRole.ActiveSecondary, cancellationToken));

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
                () => sut.ChangeRoleAsync(ReplicaRole.ActiveSecondary, cancellationToken));

            Assert.Same(expected, actual);
            Assert.Null(sut.Field<CancellationTokenSource>().Value);
            Assert.Null(sut.Field<Task>().Value);
        }

        [Fact]
        public async Task UpdatesUserServiceReplicaAddressesFromOpenedListenersWhenNewRoleIsActiveSecondary()
        {
            string name = fuzzy.String();
            string address = fuzzy.String();
            var listener = new Mock<ICommunicationListener>();
            _ = listener.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(address);
            var entry = new ServiceReplicaListener(_ => listener.Object, name, listenOnSecondary: true);
            _ = userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners()).Returns(new[] { entry });

            _ = await sut.ChangeRoleAsync(ReplicaRole.ActiveSecondary, cancellationToken);

            userServiceReplica.VerifySet(
                _ => _.Addresses = It.Is<IReadOnlyDictionary<string, string>>(d => d.Count == 0),
                Times.Once());
            userServiceReplica.VerifySet(
                _ => _.Addresses = It.Is<IReadOnlyDictionary<string, string>>(d => d.ContainsKey(name) && d[name] == address),
                Times.Once());
            userServiceReplica.VerifySet(_ => _.Addresses = It.IsAny<IReadOnlyDictionary<string, string>>(), Times.Exactly(2));
        }

        [Fact]
        public async Task DefaultsToServiceReplicaListenerInstantiateForCreatingCommunicationListeners()
        {
            var listener = new Mock<ICommunicationListener>();
            var entry = new ServiceReplicaListener(_ => listener.Object);
            _ = userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners()).Returns(new[] { entry });

            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

            CommunicationListenerInfo info = Assert.Single(sut.Field<IList<CommunicationListenerInfo>>().Value);
            Assert.Equal("default", info.Name);
            Assert.Same(typeof(TracingCommunicationListener), info.Listener.GetType());
        }

        [Fact]
        public async Task OpensListenersThatListenOnSecondaryWhenNewRoleIsActiveSecondary()
        {
            var listener = new Mock<ICommunicationListener>();
            _ = listener.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(fuzzy.String());
            var entry = new ServiceReplicaListener(_ => listener.Object, fuzzy.String(), listenOnSecondary: true);
            _ = userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners()).Returns(new[] { entry });

            _ = await sut.ChangeRoleAsync(ReplicaRole.ActiveSecondary, cancellationToken);

            listener.Verify(_ => _.OpenAsync(cancellationToken), Times.Once);
            listener.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SkipsListenersThatDoNotListenOnSecondaryWhenNewRoleIsActiveSecondary()
        {
            var listener = new Mock<ICommunicationListener>();
            var entry = new ServiceReplicaListener(_ => listener.Object, fuzzy.String(), listenOnSecondary: false);
            _ = userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners()).Returns(new[] { entry });

            _ = await sut.ChangeRoleAsync(ReplicaRole.ActiveSecondary, cancellationToken);

            listener.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Never);
            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
        }

        [Fact]
        public async Task DoesNotOpenCommunicationListenersWhenNewRoleIsNone()
        {
            _ = await sut.ChangeRoleAsync(ReplicaRole.None, cancellationToken);

            userServiceReplica.Verify(_ => _.CreateServiceReplicaListeners(), Times.Never);
            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
        }

        [Fact]
        public async Task SkipsNullReplicaListeners()
        {
            _ = userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners())
                .Returns(new ServiceReplicaListener[] { null });

            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
        }

        [Fact]
        public async Task SkipsListenersWhenCreateCommunicationListenerReturnsNull()
        {
            _ = userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners())
                .Returns(new[] { fuzzy.ServiceReplicaListener() });
            sut.Field<Func<ServiceReplicaListener, StatefulServiceContext, CommunicationListenerInfo>>()
                .Set((_, _) => null);

            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
        }

        [Fact]
        public async Task ReusesCachedReplicaListenersAcrossMultipleOpenings()
        {
            string name = fuzzy.String();
            var listener = new Mock<ICommunicationListener>();
            _ = listener.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(fuzzy.String());
            _ = userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners())
                .Returns(new[] { new ServiceReplicaListener(_ => listener.Object, name) });

            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);
            _ = await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

            userServiceReplica.Verify(_ => _.CreateServiceReplicaListeners(), Times.Once);
            listener.Verify(_ => _.OpenAsync(cancellationToken), Times.Exactly(2));
            listener.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }

    public sealed class CloseAsync : StatefulServiceReplicaAdapterTest
    {
        // Method parameters
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public async Task ClosesCommunicationListeners()
        {
            var listener = new Mock<ICommunicationListener>();
            sut.Field<IList<CommunicationListenerInfo>>().Set(new List<CommunicationListenerInfo>
            {
                new(fuzzy.String(), listener.Object),
            });

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
            sut.Field<IList<CommunicationListenerInfo>>().Set(new List<CommunicationListenerInfo>
            {
                new(fuzzy.String(), listener.Object),
            });

            await sut.CloseAsync(cancellationToken);

            listener.Verify(_ => _.Abort(), Times.Once);
            Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
        }

        [Fact]
        public async Task InvokesUserServiceOnCloseAsync()
        {
            await sut.CloseAsync(cancellationToken);
            userServiceReplica.Verify(_ => _.OnCloseAsync(cancellationToken), Times.Once);
            userServiceReplica.Verify(_ => _.OnCloseAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvokesUserServiceOnCloseAsyncBeforeStateProviderCloseAsync()
        {
            int order = 0;
            int userOrder = 0;
            int stateProviderOrder = 0;
            _ = userServiceReplica
                .Setup(_ => _.OnCloseAsync(cancellationToken))
                .Callback(() => userOrder = ++order)
                .Returns(Task.CompletedTask);
            _ = stateProvider
                .Setup(_ => _.CloseAsync(cancellationToken))
                .Callback(() => stateProviderOrder = ++order)
                .Returns(Task.CompletedTask);

            await sut.CloseAsync(cancellationToken);

            Assert.Equal(1, userOrder);
            Assert.Equal(2, stateProviderOrder);
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

        [Fact]
        public async Task ClosesStateProviderReplicaAndClearsIt()
        {
            await sut.CloseAsync(cancellationToken);

            stateProvider.Verify(_ => _.CloseAsync(cancellationToken), Times.Once);
            stateProvider.Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Null(sut.Field<IStateProviderReplica>().Value);
        }

        [Fact]
        public async Task DoesNothingToStateProviderReplicaWhenItIsNull()
        {
            sut.Field<IStateProviderReplica>().Set(null);
            await sut.CloseAsync(cancellationToken);
            Assert.Null(sut.Field<IStateProviderReplica>().Value);
        }
    }

    public sealed class Constructor : StatefulServiceReplicaAdapterTest
    {
        [Fact]
        public void ThrowsArgumentNullExceptionWhenContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new StatefulServiceReplicaAdapter(null, userServiceReplica.Object));
            Assert.Equal(typeof(StatefulServiceReplicaAdapter).Constructor().Parameter<StatefulServiceContext>().Name, exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenUserServiceReplicaIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new StatefulServiceReplicaAdapter(context, null));
            Assert.Equal(typeof(StatefulServiceReplicaAdapter).Constructor().Parameter<IStatefulUserServiceReplica>().Name, exception.ParamName);
        }

        [Fact]
        public void SetsUserServiceReplicaAddressesToEmptyReadOnlyDictionary()
        {
            userServiceReplica.VerifySet(
                _ => _.Addresses = It.Is<IReadOnlyDictionary<string, string>>(d => d.Count == 0),
                Times.Once());
            userServiceReplica.VerifySet(_ => _.Addresses = It.IsAny<IReadOnlyDictionary<string, string>>(), Times.Once());
        }

        [Fact]
        public void InvokesCreateStateProviderReplicaOnce() =>
            userServiceReplica.Verify(_ => _.CreateStateProviderReplica(), Times.Once());

        [Fact]
        public void StoresStateProviderReplicaCreatedByUserServiceReplica() =>
            Assert.Same(stateProvider.Object, sut.Field<IStateProviderReplica>().Value);
    }

    public sealed class GetStatus : StatefulServiceReplicaAdapterTest
    {
        new readonly IInternalStatefulServiceReplica sut;

        public GetStatus() => sut = (IInternalStatefulServiceReplica)base.sut;

        [Fact]
        public void ReturnsStatusFromStateProviderReplicaImplementingIInternalStatefulServiceReplica()
        {
            object expected = new();
            var stateProvider = new Mock<IStateProviderReplica>();
            _ = stateProvider.As<IInternalStatefulServiceReplica>().Setup(_ => _.GetStatus()).Returns(expected);
            base.sut.Field<IStateProviderReplica>().Set(stateProvider.Object);

            Assert.Same(expected, sut.GetStatus());
            stateProvider.As<IInternalStatefulServiceReplica>().Verify(_ => _.GetStatus(), Times.Once);
        }

        [Fact]
        public void ReturnsNullWhenStateProviderReplicaDoesNotImplementIInternalStatefulServiceReplica()
        {
            // Default mock from CreateStateProviderReplica does not implement IInternalStatefulServiceReplica
            Assert.Null(sut.GetStatus());
        }
    }

    public sealed class Initialize : StatefulServiceReplicaAdapterTest
    {
        [Fact]
        public void ForwardsToStateProviderReplica()
        {
            StatefulServiceInitializationParameters parameters = new();
            sut.Initialize(parameters);
            stateProvider.Verify(_ => _.Initialize(parameters), Times.Once);
            stateProvider.Verify(_ => _.Initialize(It.IsAny<StatefulServiceInitializationParameters>()), Times.Once);
        }
    }

    public sealed class OpenAsync : StatefulServiceReplicaAdapterTest
    {
        // Method parameters
        readonly ReplicaOpenMode openMode = fuzzy.Enum<ReplicaOpenMode>();
        readonly IStatefulServicePartition partition = Mock.Of<IStatefulServicePartition>();
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public async Task SetsServicePartition()
        {
            _ = await sut.OpenAsync(openMode, partition, cancellationToken);
            Assert.Same(partition, sut.Field<IStatefulServicePartition>().Value);
        }

        [Fact]
        public async Task SetsUserServiceReplicaPartition()
        {
            _ = await sut.OpenAsync(openMode, partition, cancellationToken);
            userServiceReplica.VerifySet(_ => _.Partition = partition, Times.Once());
            userServiceReplica.VerifySet(_ => _.Partition = It.IsAny<IStatefulServicePartition>(), Times.Once());
        }

        [Fact]
        public async Task ReturnsReplicatorFromStateProviderOpenAsync()
        {
            IReplicator expected = Mock.Of<IReplicator>();
            _ = stateProvider.Setup(_ => _.OpenAsync(openMode, partition, cancellationToken)).ReturnsAsync(expected);

            IReplicator actual = await sut.OpenAsync(openMode, partition, cancellationToken);

            Assert.Same(expected, actual);
            stateProvider.Verify(_ => _.OpenAsync(It.IsAny<ReplicaOpenMode>(), It.IsAny<IStatefulServicePartition>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvokesUserServiceOnOpenAsync()
        {
            _ = await sut.OpenAsync(openMode, partition, cancellationToken);
            userServiceReplica.Verify(_ => _.OnOpenAsync(openMode, cancellationToken), Times.Once);
            userServiceReplica.Verify(_ => _.OnOpenAsync(It.IsAny<ReplicaOpenMode>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvokesUserServiceOnOpenAsyncAfterStateProviderOpenAsync()
        {
            int order = 0;
            int stateProviderOrder = 0;
            int userOrder = 0;
            _ = stateProvider
                .Setup(_ => _.OpenAsync(openMode, partition, cancellationToken))
                .Callback(() => stateProviderOrder = ++order)
                .ReturnsAsync(Mock.Of<IReplicator>());
            _ = userServiceReplica
                .Setup(_ => _.OnOpenAsync(openMode, cancellationToken))
                .Callback(() => userOrder = ++order)
                .Returns(Task.CompletedTask);

            _ = await sut.OpenAsync(openMode, partition, cancellationToken);

            Assert.Equal(1, stateProviderOrder);
            Assert.Equal(2, userOrder);
        }

        [Fact]
        public async Task ClosesStateProviderAndRethrowsWhenOnOpenAsyncThrows()
        {
            var expected = new InvalidOperationException(fuzzy.String());
            _ = userServiceReplica.Setup(_ => _.OnOpenAsync(openMode, cancellationToken)).ThrowsAsync(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.OpenAsync(openMode, partition, cancellationToken));

            Assert.Same(expected, actual);
            stateProvider.Verify(_ => _.CloseAsync(cancellationToken), Times.Once);
            stateProvider.Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    public sealed class Test_IsRunAsyncTaskRunning : StatefulServiceReplicaAdapterTest
    {
        new readonly StatefulServiceReplicaAdapter sut;

        public Test_IsRunAsyncTaskRunning() => sut = (StatefulServiceReplicaAdapter)base.sut;

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
