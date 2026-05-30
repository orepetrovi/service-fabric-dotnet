// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Inspector;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Runtime;

public abstract class StatefulServiceBaseTest
{
    readonly TestService sut;

    // Constructor parameters
    readonly StatefulServiceContext serviceContext = fuzzy.StatefulServiceContext();
    readonly Mock<IStateProviderReplica2> stateProviderReplica = new() { DefaultValue = DefaultValue.Mock };

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    StatefulServiceBaseTest() =>
        sut = new TestService(serviceContext, stateProviderReplica.Object);

    static EventSourceTest<ServiceEventSource> InstallEventSource()
    {
        var t = new EventSourceTest<ServiceEventSource>();
        typeof(ServiceEventSource).Property<ServiceEventSource>().Set(t.Instance);
        t.EnableEvents(EventLevel.LogAlways);
        return t;
    }

    public sealed class BackupAsync_BackupDescription : StatefulServiceBaseTest
    {
        [Theory]
        [InlineData(BackupOption.Full)]
        [InlineData(BackupOption.Incremental)]
        public void ForwardsToStateProviderReplicaWithOneHourTimeoutAndNoCancellation(BackupOption option)
        {
            Func<BackupInfo, CancellationToken, Task<bool>> callback = (_, _) => Task.FromResult(true);
            var description = new BackupDescription(option, callback);
            Task expected = Task.FromResult(fuzzy.Boolean());
            _ = stateProviderReplica
                .Setup(_ => _.BackupAsync(option, TimeSpan.FromHours(1), CancellationToken.None, callback))
                .Returns(expected);

            Task actual = sut.BackupAsync(description);

            Assert.Same(expected, actual);
            stateProviderReplica.Verify(
                _ => _.BackupAsync(It.IsAny<BackupOption>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>(), It.IsAny<Func<BackupInfo, CancellationToken, Task<bool>>>()),
                Times.Once);
        }
    }

    public sealed class BackupAsync_BackupDescription_TimeSpan_CancellationToken : StatefulServiceBaseTest
    {
        [Theory]
        [InlineData(BackupOption.Full)]
        [InlineData(BackupOption.Incremental)]
        public void ForwardsArgumentsToStateProviderReplica(BackupOption option)
        {
            Func<BackupInfo, CancellationToken, Task<bool>> callback = (_, _) => Task.FromResult(true);
            var description = new BackupDescription(option, callback);
            TimeSpan timeout = TimeSpan.FromSeconds(fuzzy.Int32().Between(1, 1000));
            CancellationToken cancellation = new(canceled: true);
            Task expected = Task.FromResult(fuzzy.Boolean());
            _ = stateProviderReplica
                .Setup(_ => _.BackupAsync(option, timeout, cancellation, callback))
                .Returns(expected);

            Task actual = sut.BackupAsync(description, timeout, cancellation);

            Assert.Same(expected, actual);
            stateProviderReplica.Verify(
                _ => _.BackupAsync(It.IsAny<BackupOption>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>(), It.IsAny<Func<BackupInfo, CancellationToken, Task<bool>>>()),
                Times.Once);
        }
    }

    public sealed class Constructor : StatefulServiceBaseTest, IDisposable
    {
        // Installed before base field initializers and base ctor body so that the event raised by sut
        // construction is captured.
        readonly EventSourceTest<ServiceEventSource> events = InstallEventSource();

        void IDisposable.Dispose() => events.Dispose();

        [Fact]
        public void ThrowsArgumentNullExceptionWhenServiceContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TestService(null, stateProviderReplica.Object));
            Assert.Equal(nameof(serviceContext), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenStateProviderReplicaIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TestService(serviceContext, null));
            Assert.Equal(nameof(stateProviderReplica), exception.ParamName);
        }

        [Fact]
        public void InitializesContextWithServiceContext() =>
            Assert.Same(serviceContext, sut.Context);

        [Fact]
        public void InitializesStateProviderReplicaWithGivenInstance() =>
            Assert.Same(stateProviderReplica.Object, sut.StateProviderReplica);

        [Fact]
        public void InitializesAddressesToEmptyDictionary()
        {
            IReadOnlyDictionary<string, string> addresses = sut.GetAddressesForTest();
            Assert.Empty(addresses);
        }

        [Fact]
        public void SetsOnDataLossAsyncOnStateProviderReplica() =>
            stateProviderReplica.VerifySet(_ => _.OnDataLossAsync = It.IsAny<Func<CancellationToken, Task<bool>>>(), Times.Once);

        [Fact]
        public void SetsOnRestoreCompletedAsyncWhenStateProviderReplicaIsV2() =>
            stateProviderReplica.VerifySet(_ => _.OnRestoreCompletedAsync = It.IsAny<Func<CancellationToken, Task>>(), Times.Once);

        [Fact]
        public void InitializesWhenStateProviderReplicaIsNotV2()
        {
            var v1 = new Mock<IStateProviderReplica> { DefaultValue = DefaultValue.Mock };
            _ = new TestService(serviceContext, v1.Object);
            v1.VerifySet(_ => _.OnDataLossAsync = It.IsAny<Func<CancellationToken, Task<bool>>>(), Times.Once);
        }

        [Fact]
        public async Task SetsOnDataLossAsyncDelegateThatRoutesToProtectedOnDataLossAsync()
        {
            var replica = new Mock<IStateProviderReplica2> { DefaultValue = DefaultValue.Mock };
            Func<CancellationToken, Task<bool>> captured = null;
            _ = replica.SetupSet(_ => _.OnDataLossAsync = It.IsAny<Func<CancellationToken, Task<bool>>>())
                .Callback<Func<CancellationToken, Task<bool>>>(f => captured = f);

            CancellationToken expectedToken = new(canceled: true);
            CancellationToken actualToken = default;
            RestoreContext actualRestoreContext = default;
            Task<bool> expected = new TaskCompletionSource<bool>().Task;
            _ = new TestService(serviceContext, replica.Object)
            {
                OnDataLossAsyncHandler = (rc, ct) =>
                {
                    actualRestoreContext = rc;
                    actualToken = ct;
                    return expected;
                },
            };

            Task<bool> actual = captured(expectedToken);

            Assert.Same(expected, actual);
            Assert.Equal(expectedToken, actualToken);

            // RestoreContext is a struct without observable identity, so verify it routes to the same state
            // provider replica by invoking RestoreAsync with unique arguments and asserting the mock receives
            // that exact call.
            var description = new RestoreDescription(fuzzy.String(), RestorePolicy.Force);
            CancellationToken restoreToken = new(canceled: true);
            await actualRestoreContext.RestoreAsync(description, restoreToken);
            replica.Verify(
                _ => _.RestoreAsync(description.BackupFolderPath, description.Policy, restoreToken),
                Times.Once);
        }

        [Fact]
        public void SetsOnRestoreCompletedAsyncDelegateThatRoutesToProtectedOnRestoreCompletedAsync()
        {
            var replica = new Mock<IStateProviderReplica2> { DefaultValue = DefaultValue.Mock };
            Func<CancellationToken, Task> captured = null;
            _ = replica.SetupSet(_ => _.OnRestoreCompletedAsync = It.IsAny<Func<CancellationToken, Task>>())
                .Callback<Func<CancellationToken, Task>>(f => captured = f);

            CancellationToken expectedToken = new(canceled: true);
            CancellationToken actualToken = default;
            Task expected = new TaskCompletionSource<int>().Task;
            _ = new TestService(serviceContext, replica.Object)
            {
                OnRestoreCompletedAsyncHandler = ct => { actualToken = ct; return expected; },
            };

            Task actual = captured(expectedToken);

            Assert.Same(expected, actual);
            Assert.Equal(expectedToken, actualToken);
        }

        [Fact]
        public void RaisesStatefulServiceInitializeEvent()
        {
            Assert.NotNull(events.Event);
            Assert.Equal("ServiceLifecycleEvent", events.Event.EventName);
            events.EventPayload(3, "partitionId", serviceContext.PartitionId.ToString());
            events.EventPayload(4, "replicaOrInstanceId", serviceContext.ReplicaId.ToString());
            events.EventPayload(9, "lifecycleEvent", TelemetryConstants.LifecycleEventOpened);
            events.EventPayload(10, "serviceKind", TelemetryConstants.StatefulServiceKind);
        }
    }

    public sealed class IStatefulUserServiceReplica_Addresses : StatefulServiceBaseTest
    {
        [Fact]
        public void UpdatesValueReturnedByGetAddresses()
        {
            IReadOnlyDictionary<string, string> addresses = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string> { { fuzzy.String(), fuzzy.String() } });

            ((IStatefulUserServiceReplica)sut).Addresses = addresses;

            Assert.Same(addresses, sut.GetAddressesForTest());
        }
    }

    public sealed class IStatefulUserServiceReplica_CreateServiceReplicaListeners : StatefulServiceBaseTest
    {
        [Fact]
        public void ReturnsEmptyByDefault() =>
            Assert.Empty(((IStatefulUserServiceReplica)sut).CreateServiceReplicaListeners());

        [Fact]
        public void ForwardsToProtectedCreateServiceReplicaListeners()
        {
            IEnumerable<ServiceReplicaListener> expected = fuzzy.Array(fuzzy.ServiceReplicaListener);
            sut.CreateServiceReplicaListenersHandler = () => expected;

            IEnumerable<ServiceReplicaListener> actual = ((IStatefulUserServiceReplica)sut).CreateServiceReplicaListeners();

            Assert.Same(expected, actual);
        }
    }

    public sealed class IStatefulUserServiceReplica_CreateStateProviderReplica : StatefulServiceBaseTest
    {
        [Fact]
        public void ReturnsStateProviderReplicaPassedToConstructor() =>
            Assert.Same(stateProviderReplica.Object, ((IStatefulUserServiceReplica)sut).CreateStateProviderReplica());
    }

    public sealed class IStatefulUserServiceReplica_OnAbort : StatefulServiceBaseTest
    {
        [Fact]
        public void DoesNothingByDefault() =>
            ((IStatefulUserServiceReplica)sut).OnAbort();

        [Fact]
        public void ForwardsToProtectedOnAbort()
        {
            bool called = false;
            sut.OnAbortHandler = () => called = true;

            ((IStatefulUserServiceReplica)sut).OnAbort();

            Assert.True(called);
        }
    }

    public sealed class IStatefulUserServiceReplica_OnChangeRoleAsync : StatefulServiceBaseTest
    {
        [Fact]
        public void ReturnsCompletedTaskByDefault()
        {
            Task actual = ((IStatefulUserServiceReplica)sut).OnChangeRoleAsync(ReplicaRole.Primary, TestContext.Current.CancellationToken);
            Assert.True(actual.IsCompletedSuccessfully);
        }

        [Theory]
        [InlineData(ReplicaRole.Primary)]
        [InlineData(ReplicaRole.IdleSecondary)]
        [InlineData(ReplicaRole.ActiveSecondary)]
        [InlineData(ReplicaRole.None)]
        public void ForwardsArgumentsToProtectedOnChangeRoleAsync(ReplicaRole expectedRole)
        {
            CancellationToken expectedToken = new(canceled: true);
            ReplicaRole actualRole = default;
            CancellationToken actualToken = default;
            Task task = new TaskCompletionSource<int>().Task;
            sut.OnChangeRoleAsyncHandler = (r, ct) => { actualRole = r; actualToken = ct; return task; };

            Task result = ((IStatefulUserServiceReplica)sut).OnChangeRoleAsync(expectedRole, expectedToken);

            Assert.Same(task, result);
            Assert.Equal(expectedRole, actualRole);
            Assert.Equal(expectedToken, actualToken);
        }
    }

    public sealed class IStatefulUserServiceReplica_OnCloseAsync : StatefulServiceBaseTest, IDisposable
    {
        readonly EventSourceTest<ServiceEventSource> events = InstallEventSource();

        void IDisposable.Dispose() => events.Dispose();

        [Fact]
        public void ReturnsCompletedTaskByDefault()
        {
            Task actual = ((IStatefulUserServiceReplica)sut).OnCloseAsync(TestContext.Current.CancellationToken);
            Assert.True(actual.IsCompletedSuccessfully);
        }

        [Fact]
        public void ForwardsCancellationTokenToProtectedOnCloseAsync()
        {
            CancellationToken expected = new(canceled: true);
            CancellationToken actual = default;
            Task task = new TaskCompletionSource<int>().Task;
            sut.OnCloseAsyncHandler = ct => { actual = ct; return task; };

            Task result = ((IStatefulUserServiceReplica)sut).OnCloseAsync(expected);

            Assert.Same(task, result);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task RaisesStatefulServiceReplicaCloseEvent()
        {
            await ((IStatefulUserServiceReplica)sut).OnCloseAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(events.Event);
            Assert.Equal("ServiceLifecycleEvent", events.Event.EventName);
            events.EventPayload(3, "partitionId", serviceContext.PartitionId.ToString());
            events.EventPayload(4, "replicaOrInstanceId", serviceContext.ReplicaId.ToString());
            events.EventPayload(9, "lifecycleEvent", TelemetryConstants.LifecycleEventClosed);
            events.EventPayload(10, "serviceKind", TelemetryConstants.StatefulServiceKind);
        }
    }

    public sealed class IStatefulUserServiceReplica_OnOpenAsync : StatefulServiceBaseTest
    {
        [Fact]
        public void ReturnsCompletedTaskByDefault()
        {
            Task actual = ((IStatefulUserServiceReplica)sut).OnOpenAsync(ReplicaOpenMode.New, TestContext.Current.CancellationToken);
            Assert.True(actual.IsCompletedSuccessfully);
        }

        [Theory]
        [InlineData(ReplicaOpenMode.New)]
        [InlineData(ReplicaOpenMode.Existing)]
        public void ForwardsArgumentsToProtectedOnOpenAsync(ReplicaOpenMode expectedMode)
        {
            CancellationToken expectedToken = new(canceled: true);
            ReplicaOpenMode actualMode = default;
            CancellationToken actualToken = default;
            Task task = new TaskCompletionSource<int>().Task;
            sut.OnOpenAsyncHandler = (m, ct) => { actualMode = m; actualToken = ct; return task; };

            Task result = ((IStatefulUserServiceReplica)sut).OnOpenAsync(expectedMode, expectedToken);

            Assert.Same(task, result);
            Assert.Equal(expectedMode, actualMode);
            Assert.Equal(expectedToken, actualToken);
        }
    }

    public sealed class IStatefulUserServiceReplica_RunAsync : StatefulServiceBaseTest
    {
        [Fact]
        public void ReturnsCompletedTaskByDefault()
        {
            Task actual = ((IStatefulUserServiceReplica)sut).RunAsync(TestContext.Current.CancellationToken);
            Assert.True(actual.IsCompletedSuccessfully);
        }

        [Fact]
        public void ForwardsCancellationTokenToProtectedRunAsync()
        {
            CancellationToken expected = new(canceled: true);
            CancellationToken actual = default;
            Task task = new TaskCompletionSource<int>().Task;
            sut.RunAsyncHandler = ct => { actual = ct; return task; };

            Task result = ((IStatefulUserServiceReplica)sut).RunAsync(expected);

            Assert.Same(task, result);
            Assert.Equal(expected, actual);
        }
    }

    public sealed class OnDataLossAsync : StatefulServiceBaseTest
    {
        [Fact]
        public async Task ReturnsFalse() =>
            Assert.False(await sut.InvokeBaseOnDataLossAsync(default, TestContext.Current.CancellationToken));
    }

    public sealed class OnRestoreCompletedAsync : StatefulServiceBaseTest
    {
        [Fact]
        public void ReturnsCompletedTask()
        {
            Task actual = sut.InvokeBaseOnRestoreCompletedAsync(TestContext.Current.CancellationToken);
            Assert.True(actual.IsCompletedSuccessfully);
        }
    }

    public sealed class Partition : StatefulServiceBaseTest
    {
        [Fact]
        public void IsInitiallyNull() =>
            Assert.Null(sut.GetPartitionForTest());

        [Fact]
        public void IsSetByExplicitInterfaceSetter()
        {
            var partition = Mock.Of<IStatefulServicePartition>();
            ((IStatefulUserServiceReplica)sut).Partition = partition;
            Assert.Same(partition, sut.GetPartitionForTest());
        }
    }

    public sealed class ServiceContext : StatefulServiceBaseTest
    {
        [Fact]
        public void ReturnsServiceContextPassedToConstructor() =>
            Assert.Same(serviceContext, sut.GetServiceContextForTest());
    }

    sealed class TestService : StatefulServiceBase
    {
        internal TestService(StatefulServiceContext serviceContext, IStateProviderReplica stateProviderReplica)
            : base(serviceContext, stateProviderReplica) { }

        // Hooks that override base protected virtuals when assigned; otherwise the base implementation runs.
        internal Func<CancellationToken, Task> RunAsyncHandler;
        internal Func<ReplicaOpenMode, CancellationToken, Task> OnOpenAsyncHandler;
        internal Func<ReplicaRole, CancellationToken, Task> OnChangeRoleAsyncHandler;
        internal Func<CancellationToken, Task> OnCloseAsyncHandler;
        internal Action OnAbortHandler;
        internal Func<IEnumerable<ServiceReplicaListener>> CreateServiceReplicaListenersHandler;
        internal Func<RestoreContext, CancellationToken, Task<bool>> OnDataLossAsyncHandler;
        internal Func<CancellationToken, Task> OnRestoreCompletedAsyncHandler;

        protected override Task RunAsync(CancellationToken cancellationToken) =>
            RunAsyncHandler != null ? RunAsyncHandler(cancellationToken) : base.RunAsync(cancellationToken);

        protected override Task OnOpenAsync(ReplicaOpenMode openMode, CancellationToken cancellationToken) =>
            OnOpenAsyncHandler != null ? OnOpenAsyncHandler(openMode, cancellationToken) : base.OnOpenAsync(openMode, cancellationToken);

        protected override Task OnChangeRoleAsync(ReplicaRole newRole, CancellationToken cancellationToken) =>
            OnChangeRoleAsyncHandler != null ? OnChangeRoleAsyncHandler(newRole, cancellationToken) : base.OnChangeRoleAsync(newRole, cancellationToken);

        protected override Task OnCloseAsync(CancellationToken cancellationToken) =>
            OnCloseAsyncHandler != null ? OnCloseAsyncHandler(cancellationToken) : base.OnCloseAsync(cancellationToken);

        protected override void OnAbort()
        {
            if (OnAbortHandler != null)
                OnAbortHandler();
            else
                base.OnAbort();
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners() =>
            CreateServiceReplicaListenersHandler != null ? CreateServiceReplicaListenersHandler() : base.CreateServiceReplicaListeners();

        protected override Task<bool> OnDataLossAsync(RestoreContext restoreCtx, CancellationToken cancellationToken) =>
            OnDataLossAsyncHandler != null ? OnDataLossAsyncHandler(restoreCtx, cancellationToken) : base.OnDataLossAsync(restoreCtx, cancellationToken);

        protected override Task OnRestoreCompletedAsync(CancellationToken cancellationToken) =>
            OnRestoreCompletedAsyncHandler != null ? OnRestoreCompletedAsyncHandler(cancellationToken) : base.OnRestoreCompletedAsync(cancellationToken);

        internal IReadOnlyDictionary<string, string> GetAddressesForTest() => GetAddresses();
        internal IStatefulServicePartition GetPartitionForTest() => Partition;
        internal StatefulServiceContext GetServiceContextForTest() => ServiceContext;
        internal Task<bool> InvokeBaseOnDataLossAsync(RestoreContext c, CancellationToken ct) => base.OnDataLossAsync(c, ct);
        internal Task InvokeBaseOnRestoreCompletedAsync(CancellationToken ct) => base.OnRestoreCompletedAsync(ct);
    }
}
