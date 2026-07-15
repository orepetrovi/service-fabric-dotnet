// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
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

    public sealed class BackupAsync_BackupDescription : StatefulServiceBaseTest
    {
        readonly Func<BackupInfo, CancellationToken, Task<bool>> callback = (_, _) => Task.FromResult(true);

        [Theory]
        [InlineData(BackupOption.Full)]
        [InlineData(BackupOption.Incremental)]
        public void ForwardsToStateProviderReplicaWithOneHourTimeoutAndNoCancellation(BackupOption option)
        {
            var description = new BackupDescription(option, callback);
            Task expected = new TaskCompletionSource<bool>().Task;
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
        readonly TimeSpan timeout = fuzzy.TimeSpan();
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        readonly Func<BackupInfo, CancellationToken, Task<bool>> callback = (_, _) => Task.FromResult(true);

        [Theory]
        [InlineData(BackupOption.Full)]
        [InlineData(BackupOption.Incremental)]
        public void ForwardsArgumentsToStateProviderReplica(BackupOption option)
        {
            var description = new BackupDescription(option, callback);
            Task expected = new TaskCompletionSource<bool>().Task;
            _ = stateProviderReplica
                .Setup(_ => _.BackupAsync(option, timeout, cancellationToken, callback))
                .Returns(expected);

            Task actual = sut.BackupAsync(description, timeout, cancellationToken);

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
        public void InitializesProperties()
        {
            Assert.Same(serviceContext, sut.Context);
            Assert.Same(serviceContext, sut.GetServiceContextForTest());
            Assert.Same(stateProviderReplica.Object, sut.StateProviderReplica);
            Assert.Null(sut.GetPartitionForTest());
            Assert.Empty(sut.GetAddressesForTest());
        }

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
        public void SetsOnDataLossAsyncDelegateWhenStateProviderReplicaIsNotV2()
        {
            var replica = new Mock<IStateProviderReplica> { DefaultValue = DefaultValue.Mock };

            _ = new TestService(serviceContext, replica.Object);

            replica.VerifySet(_ => _.OnDataLossAsync = It.IsAny<Func<CancellationToken, Task<bool>>>(), Times.Once);
        }

        [Theory]
        [InlineData(RestorePolicy.Safe)]
        [InlineData(RestorePolicy.Force)]
        public void SetsOnDataLossAsyncDelegateThatRoutesToProtectedOnDataLossAsync(RestorePolicy policy)
        {
            var replica = new Mock<IStateProviderReplica2> { DefaultValue = DefaultValue.Mock };
            Func<CancellationToken, Task<bool>> captured = null;
            _ = replica.SetupSet(_ => _.OnDataLossAsync = It.IsAny<Func<CancellationToken, Task<bool>>>())
                .Callback<Func<CancellationToken, Task<bool>>>(f => captured = f);

            CancellationToken expectedToken = TestContext.Current.CancellationToken;
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
            var description = new RestoreDescription(fuzzy.String(), policy);
            CancellationToken restoreToken = new CancellationTokenSource().Token;
            _ = actualRestoreContext.RestoreAsync(description, restoreToken);
            replica.Verify(
                _ => _.RestoreAsync(description.BackupFolderPath, description.Policy, restoreToken),
                Times.Once);
            replica.Verify(
                _ => _.RestoreAsync(It.IsAny<string>(), It.IsAny<RestorePolicy>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void SetsOnRestoreCompletedAsyncDelegateThatRoutesToProtectedOnRestoreCompletedAsync()
        {
            var replica = new Mock<IStateProviderReplica2> { DefaultValue = DefaultValue.Mock };
            Func<CancellationToken, Task> captured = null;
            _ = replica.SetupSet(_ => _.OnRestoreCompletedAsync = It.IsAny<Func<CancellationToken, Task>>())
                .Callback<Func<CancellationToken, Task>>(f => captured = f);

            CancellationToken expectedToken = TestContext.Current.CancellationToken;
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

    public sealed class CreateServiceReplicaListeners : StatefulServiceBaseTest
    {
        [Fact]
        public void ReturnsEmptyByDefault() =>
            Assert.Empty(sut.InvokeBaseCreateServiceReplicaListeners());
    }

    public abstract class IStatefulUserServiceReplicaTest : StatefulServiceBaseTest
    {
        private protected new readonly IStatefulUserServiceReplica sut;

        private protected IStatefulUserServiceReplicaTest() => sut = base.sut;
    }

    public sealed class IStatefulUserServiceReplica_Addresses : IStatefulUserServiceReplicaTest
    {
        // Method parameters
        readonly IReadOnlyDictionary<string, string> addresses = fuzzy.Dictionary(fuzzy.String, fuzzy.String);

        [Fact]
        public void UpdatesValueReturnedByGetAddresses()
        {
            sut.Addresses = addresses;

            Assert.Same(addresses, ((TestService)sut).GetAddressesForTest());
        }
    }

    public sealed class IStatefulUserServiceReplica_CreateServiceReplicaListeners : IStatefulUserServiceReplicaTest
    {
        [Fact]
        public void ForwardsToProtectedCreateServiceReplicaListeners()
        {
            IEnumerable<ServiceReplicaListener> expected = fuzzy.Array(fuzzy.ServiceReplicaListener);
            int calls = 0;
            ((TestService)sut).CreateServiceReplicaListenersHandler = () => { calls++; return expected; };

            IEnumerable<ServiceReplicaListener> actual = sut.CreateServiceReplicaListeners();

            Assert.Same(expected, actual);
            Assert.Equal(1, calls);
        }
    }

    public sealed class IStatefulUserServiceReplica_CreateStateProviderReplica : IStatefulUserServiceReplicaTest
    {
        [Fact]
        public void ReturnsStateProviderReplicaPassedToConstructor() =>
            Assert.Same(stateProviderReplica.Object, sut.CreateStateProviderReplica());
    }

    public sealed class IStatefulUserServiceReplica_OnAbort : IStatefulUserServiceReplicaTest
    {
        [Fact]
        public void ForwardsToProtectedOnAbort()
        {
            int calls = 0;
            ((TestService)sut).OnAbortHandler = () => calls++;

            sut.OnAbort();

            Assert.Equal(1, calls);
        }
    }

    public sealed class IStatefulUserServiceReplica_OnChangeRoleAsync : IStatefulUserServiceReplicaTest
    {
        // Method parameters
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(ReplicaRole.Primary)]
        [InlineData(ReplicaRole.IdleSecondary)]
        [InlineData(ReplicaRole.ActiveSecondary)]
        [InlineData(ReplicaRole.None)]
        public void ForwardsArgumentsToProtectedOnChangeRoleAsync(ReplicaRole newRole)
        {
            ReplicaRole actualRole = default;
            CancellationToken actualToken = default;
            int calls = 0;
            Task task = new TaskCompletionSource<int>().Task;
            ((TestService)sut).OnChangeRoleAsyncHandler = (r, ct) => { calls++; actualRole = r; actualToken = ct; return task; };

            Task result = sut.OnChangeRoleAsync(newRole, cancellationToken);

            Assert.Same(task, result);
            Assert.Equal(newRole, actualRole);
            Assert.Equal(cancellationToken, actualToken);
            Assert.Equal(1, calls);
        }
    }

    public sealed class IStatefulUserServiceReplica_OnCloseAsync : IStatefulUserServiceReplicaTest, IDisposable
    {
        // Method parameters
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        readonly EventSourceTest<ServiceEventSource> events = InstallEventSource();

        void IDisposable.Dispose() => events.Dispose();

        [Fact]
        public void ForwardsToProtectedOnCloseAsync()
        {
            CancellationToken actual = default;
            int calls = 0;
            Task task = new TaskCompletionSource<int>().Task;
            ((TestService)sut).OnCloseAsyncHandler = ct => { calls++; actual = ct; return task; };

            Task result = sut.OnCloseAsync(cancellationToken);

            Assert.Same(task, result);
            Assert.Equal(cancellationToken, actual);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void RaisesStatefulServiceReplicaCloseEvent()
        {
            _ = sut.OnCloseAsync(cancellationToken);

            Assert.NotNull(events.Event);
            Assert.Equal("ServiceLifecycleEvent", events.Event.EventName);
            events.EventPayload(3, "partitionId", serviceContext.PartitionId.ToString());
            events.EventPayload(4, "replicaOrInstanceId", serviceContext.ReplicaId.ToString());
            events.EventPayload(9, "lifecycleEvent", TelemetryConstants.LifecycleEventClosed);
            events.EventPayload(10, "serviceKind", TelemetryConstants.StatefulServiceKind);
        }
    }

    public sealed class IStatefulUserServiceReplica_OnOpenAsync : IStatefulUserServiceReplicaTest
    {
        // Method parameters
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(ReplicaOpenMode.New)]
        [InlineData(ReplicaOpenMode.Existing)]
        public void ForwardsArgumentsToProtectedOnOpenAsync(ReplicaOpenMode openMode)
        {
            ReplicaOpenMode actualMode = default;
            CancellationToken actualToken = default;
            int calls = 0;
            Task task = new TaskCompletionSource<int>().Task;
            ((TestService)sut).OnOpenAsyncHandler = (m, ct) => { calls++; actualMode = m; actualToken = ct; return task; };

            Task result = sut.OnOpenAsync(openMode, cancellationToken);

            Assert.Same(task, result);
            Assert.Equal(openMode, actualMode);
            Assert.Equal(cancellationToken, actualToken);
            Assert.Equal(1, calls);
        }
    }

    public sealed class IStatefulUserServiceReplica_Partition : IStatefulUserServiceReplicaTest
    {
        // Method parameters
        readonly IStatefulServicePartition partition = Mock.Of<IStatefulServicePartition>();

        [Fact]
        public void UpdatesValueReturnedByGetPartition()
        {
            sut.Partition = partition;

            Assert.Same(partition, ((TestService)sut).GetPartitionForTest());
        }
    }

    public sealed class IStatefulUserServiceReplica_RunAsync : IStatefulUserServiceReplicaTest
    {
        // Method parameters
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public void ForwardsToProtectedRunAsync()
        {
            CancellationToken actual = default;
            int calls = 0;
            Task task = new TaskCompletionSource<int>().Task;
            ((TestService)sut).RunAsyncHandler = ct => { calls++; actual = ct; return task; };

            Task result = sut.RunAsync(cancellationToken);

            Assert.Same(task, result);
            Assert.Equal(cancellationToken, actual);
            Assert.Equal(1, calls);
        }
    }

    public sealed class OnAbort : StatefulServiceBaseTest
    {
        [Fact]
        public void DoesNothingByDefault() =>
            sut.InvokeBaseOnAbort();
    }

    public sealed class OnChangeRoleAsync : StatefulServiceBaseTest
    {
        [Fact]
        public void ReturnsCompletedTaskByDefault()
        {
            Task actual = sut.InvokeBaseOnChangeRoleAsync(fuzzy.Enum<ReplicaRole>(), TestContext.Current.CancellationToken);
            Assert.Equal(TaskStatus.RanToCompletion, actual.Status);
        }
    }

    public sealed class OnCloseAsync : StatefulServiceBaseTest
    {
        [Fact]
        public void ReturnsCompletedTaskByDefault()
        {
            Task actual = sut.InvokeBaseOnCloseAsync(TestContext.Current.CancellationToken);
            Assert.Equal(TaskStatus.RanToCompletion, actual.Status);
        }
    }

    public sealed class OnDataLossAsync : StatefulServiceBaseTest
    {
        // Method parameters
        readonly RestoreContext restoreCtx = default;
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public async Task ReturnsFalse() =>
            Assert.False(await sut.InvokeBaseOnDataLoss(restoreCtx, cancellationToken));
    }

    public sealed class OnOpenAsync : StatefulServiceBaseTest
    {
        [Fact]
        public void ReturnsCompletedTaskByDefault()
        {
            Task actual = sut.InvokeBaseOnOpenAsync(fuzzy.Enum<ReplicaOpenMode>(), TestContext.Current.CancellationToken);
            Assert.Equal(TaskStatus.RanToCompletion, actual.Status);
        }
    }

    public sealed class OnRestoreCompletedAsync : StatefulServiceBaseTest
    {
        // Method parameters
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public void ReturnsCompletedTask()
        {
            Task actual = sut.InvokeBaseOnRestoreCompleted(cancellationToken);
            Assert.Equal(TaskStatus.RanToCompletion, actual.Status);
        }
    }

    public sealed class RunAsync : StatefulServiceBaseTest
    {
        [Fact]
        public void ReturnsCompletedTaskByDefault()
        {
            Task actual = sut.InvokeBaseRunAsync(TestContext.Current.CancellationToken);
            Assert.Equal(TaskStatus.RanToCompletion, actual.Status);
        }
    }

    static EventSourceTest<ServiceEventSource> InstallEventSource()
    {
        var t = new EventSourceTest<ServiceEventSource>();
        typeof(ServiceEventSource).Property<ServiceEventSource>().Set(t.Instance);
        t.EnableEvents(EventLevel.LogAlways);
        return t;
    }

    sealed class TestService(StatefulServiceContext serviceContext, IStateProviderReplica stateProviderReplica)
        : StatefulServiceBase(serviceContext, stateProviderReplica)
    {
        // Hooks that override base protected virtuals when assigned; otherwise the base implementation runs.
        internal Func<CancellationToken, Task> RunAsyncHandler;
        internal Func<ReplicaOpenMode, CancellationToken, Task> OnOpenAsyncHandler;
        internal Func<ReplicaRole, CancellationToken, Task> OnChangeRoleAsyncHandler;
        internal Func<CancellationToken, Task> OnCloseAsyncHandler;
        internal Action OnAbortHandler;
        internal Func<IEnumerable<ServiceReplicaListener>> CreateServiceReplicaListenersHandler;
        internal Func<RestoreContext, CancellationToken, Task<bool>> OnDataLossAsyncHandler;
        internal Func<CancellationToken, Task> OnRestoreCompletedAsyncHandler;

        protected override Task RunAsync(CancellationToken cancellation) =>
            RunAsyncHandler != null ? RunAsyncHandler(cancellation) : base.RunAsync(cancellation);

        protected override Task OnOpenAsync(ReplicaOpenMode openMode, CancellationToken cancellation) =>
            OnOpenAsyncHandler != null ? OnOpenAsyncHandler(openMode, cancellation) : base.OnOpenAsync(openMode, cancellation);

        protected override Task OnChangeRoleAsync(ReplicaRole newRole, CancellationToken cancellation) =>
            OnChangeRoleAsyncHandler != null ? OnChangeRoleAsyncHandler(newRole, cancellation) : base.OnChangeRoleAsync(newRole, cancellation);

        protected override Task OnCloseAsync(CancellationToken cancellation) =>
            OnCloseAsyncHandler != null ? OnCloseAsyncHandler(cancellation) : base.OnCloseAsync(cancellation);

        protected override void OnAbort()
        {
            if (OnAbortHandler != null)
                OnAbortHandler();
            else
                base.OnAbort();
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners() =>
            CreateServiceReplicaListenersHandler != null ? CreateServiceReplicaListenersHandler() : base.CreateServiceReplicaListeners();

        protected override Task<bool> OnDataLossAsync(RestoreContext restoreCtx, CancellationToken cancellation) =>
            OnDataLossAsyncHandler != null ? OnDataLossAsyncHandler(restoreCtx, cancellation) : base.OnDataLossAsync(restoreCtx, cancellation);

        protected override Task OnRestoreCompletedAsync(CancellationToken cancellation) =>
            OnRestoreCompletedAsyncHandler != null ? OnRestoreCompletedAsyncHandler(cancellation) : base.OnRestoreCompletedAsync(cancellation);

        internal IReadOnlyDictionary<string, string> GetAddressesForTest() => GetAddresses();
        internal IStatefulServicePartition GetPartitionForTest() => Partition;
        internal StatefulServiceContext GetServiceContextForTest() => ServiceContext;
        internal Task<bool> InvokeBaseOnDataLoss(RestoreContext restoreCtx, CancellationToken cancellation) => base.OnDataLossAsync(restoreCtx, cancellation);
        internal Task InvokeBaseOnRestoreCompleted(CancellationToken cancellation) => base.OnRestoreCompletedAsync(cancellation);
        internal Task InvokeBaseRunAsync(CancellationToken cancellation) => base.RunAsync(cancellation);
        internal Task InvokeBaseOnOpenAsync(ReplicaOpenMode openMode, CancellationToken cancellation) => base.OnOpenAsync(openMode, cancellation);
        internal Task InvokeBaseOnChangeRoleAsync(ReplicaRole newRole, CancellationToken cancellation) => base.OnChangeRoleAsync(newRole, cancellation);
        internal Task InvokeBaseOnCloseAsync(CancellationToken cancellation) => base.OnCloseAsync(cancellation);
        internal void InvokeBaseOnAbort() => base.OnAbort();
        internal IEnumerable<ServiceReplicaListener> InvokeBaseCreateServiceReplicaListeners() => base.CreateServiceReplicaListeners();
    }
}
