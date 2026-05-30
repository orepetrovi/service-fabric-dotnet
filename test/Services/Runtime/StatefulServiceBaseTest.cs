// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Runtime
{
    public abstract class StatefulServiceBaseTest
    {
        readonly TestService sut;

        // Constructor parameters
        readonly StatefulServiceContext serviceContext = fuzzy.StatefulServiceContext();
        readonly Mock<IStateProviderReplica2> stateProviderReplica = new() { DefaultValue = DefaultValue.Mock };

        static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

        StatefulServiceBaseTest() =>
            sut = new TestService(serviceContext, stateProviderReplica.Object);

        public sealed class Constructor_StatefulServiceContext_IStateProviderReplica : StatefulServiceBaseTest
        {
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
            public void DoesNotSetOnRestoreCompletedAsyncWhenStateProviderReplicaIsNotV2()
            {
                var v1 = new Mock<IStateProviderReplica> { DefaultValue = DefaultValue.Mock };
                _ = new TestService(serviceContext, v1.Object);
                v1.VerifySet(_ => _.OnDataLossAsync = It.IsAny<Func<CancellationToken, Task<bool>>>(), Times.Once);
                // No OnRestoreCompletedAsync setter exists on IStateProviderReplica, so the only assertion is that
                // construction succeeded without attempting to cast.
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
                var service = new TestService(serviceContext, replica.Object)
                {
                    OnDataLossAsyncHandler = (_, ct) => { actualToken = ct; return Task.FromResult(true); },
                };

                bool result = await captured(expectedToken);

                Assert.True(result);
                Assert.Equal(expectedToken, actualToken);
            }

            [Fact]
            public async Task SetsOnRestoreCompletedAsyncDelegateThatRoutesToProtectedOnRestoreCompletedAsync()
            {
                var replica = new Mock<IStateProviderReplica2> { DefaultValue = DefaultValue.Mock };
                Func<CancellationToken, Task> captured = null;
                _ = replica.SetupSet(_ => _.OnRestoreCompletedAsync = It.IsAny<Func<CancellationToken, Task>>())
                    .Callback<Func<CancellationToken, Task>>(f => captured = f);

                CancellationToken expectedToken = new(canceled: true);
                CancellationToken actualToken = default;
                var service = new TestService(serviceContext, replica.Object)
                {
                    OnRestoreCompletedAsyncHandler = ct => { actualToken = ct; return Task.CompletedTask; },
                };

                await captured(expectedToken);

                Assert.Equal(expectedToken, actualToken);
            }

            [Fact(Explicit = true)] // TODO: SUT testability limitation. ServiceTelemetry is a static class; its events can't be intercepted without changes to StatefulServiceBase.
            public void RaisesStatefulServiceInitializeEvent() =>
                throw new NotImplementedException(
                    "StatefulServiceBase's constructor calls ServiceTelemetry.StatefulServiceInitializeEvent. " +
                    "ServiceTelemetry is a static class so the invocation cannot be intercepted from a unit test " +
                    "without testability changes to the SUT.");
        }

        public sealed class ServiceContext_Property : StatefulServiceBaseTest
        {
            [Fact]
            public void ReturnsServiceContextPassedToConstructor() =>
                Assert.Same(serviceContext, sut.GetServiceContextForTest());
        }

        public sealed class Partition_Property : StatefulServiceBaseTest
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

        public sealed class Addresses_Setter : StatefulServiceBaseTest
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

        public sealed class BackupAsync_BackupDescription : StatefulServiceBaseTest
        {
            [Theory]
            [InlineData(BackupOption.Full)]
            [InlineData(BackupOption.Incremental)]
            public async Task ForwardsToStateProviderReplicaWithOneHourTimeoutAndNoCancellation(BackupOption option)
            {
                Func<BackupInfo, CancellationToken, Task<bool>> callback = (_, _) => Task.FromResult(true);
                var description = new BackupDescription(option, callback);
                Task expected = Task.FromResult(fuzzy.Boolean());
                _ = stateProviderReplica
                    .Setup(_ => _.BackupAsync(option, TimeSpan.FromHours(1), CancellationToken.None, callback))
                    .Returns(expected);

                Task actual = sut.BackupAsync(description);

                Assert.Same(expected, actual);
                await actual;
                stateProviderReplica.Verify(
                    _ => _.BackupAsync(It.IsAny<BackupOption>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>(), It.IsAny<Func<BackupInfo, CancellationToken, Task<bool>>>()),
                    Times.Once);
            }
        }

        public sealed class BackupAsync_BackupDescription_TimeSpan_CancellationToken : StatefulServiceBaseTest
        {
            [Fact]
            public async Task ForwardsArgumentsToStateProviderReplica()
            {
                BackupOption option = fuzzy.Enum<BackupOption>();
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
                await actual;
                stateProviderReplica.Verify(
                    _ => _.BackupAsync(It.IsAny<BackupOption>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>(), It.IsAny<Func<BackupInfo, CancellationToken, Task<bool>>>()),
                    Times.Once);
            }
        }

        public sealed class IStatefulUserServiceReplica_CreateStateProviderReplica : StatefulServiceBaseTest
        {
            [Fact]
            public void ReturnsStateProviderReplicaPassedToConstructor() =>
                Assert.Same(stateProviderReplica.Object, ((IStatefulUserServiceReplica)sut).CreateStateProviderReplica());
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

        public sealed class IStatefulUserServiceReplica_RunAsync : StatefulServiceBaseTest
        {
            [Fact]
            public async Task ReturnsCompletedTaskByDefault()
            {
                Task actual = ((IStatefulUserServiceReplica)sut).RunAsync(TestContext.Current.CancellationToken);
                await actual;
                Assert.True(actual.IsCompletedSuccessfully);
            }

            [Fact]
            public async Task ForwardsCancellationTokenToProtectedRunAsync()
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

        public sealed class IStatefulUserServiceReplica_OnOpenAsync : StatefulServiceBaseTest
        {
            [Fact]
            public async Task ReturnsCompletedTaskByDefault()
            {
                Task actual = ((IStatefulUserServiceReplica)sut).OnOpenAsync(ReplicaOpenMode.New, TestContext.Current.CancellationToken);
                await actual;
                Assert.True(actual.IsCompletedSuccessfully);
            }

            [Fact]
            public async Task ForwardsArgumentsToProtectedOnOpenAsync()
            {
                ReplicaOpenMode expectedMode = ReplicaOpenMode.Existing;
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

        public sealed class IStatefulUserServiceReplica_OnChangeRoleAsync : StatefulServiceBaseTest
        {
            [Fact]
            public async Task ReturnsCompletedTaskByDefault()
            {
                Task actual = ((IStatefulUserServiceReplica)sut).OnChangeRoleAsync(ReplicaRole.Primary, TestContext.Current.CancellationToken);
                await actual;
                Assert.True(actual.IsCompletedSuccessfully);
            }

            [Fact]
            public async Task ForwardsArgumentsToProtectedOnChangeRoleAsync()
            {
                ReplicaRole expectedRole = ReplicaRole.ActiveSecondary;
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

        public sealed class IStatefulUserServiceReplica_OnCloseAsync : StatefulServiceBaseTest
        {
            [Fact]
            public async Task ReturnsCompletedTaskByDefault()
            {
                Task actual = ((IStatefulUserServiceReplica)sut).OnCloseAsync(TestContext.Current.CancellationToken);
                await actual;
                Assert.True(actual.IsCompletedSuccessfully);
            }

            [Fact]
            public async Task ForwardsCancellationTokenToProtectedOnCloseAsync()
            {
                CancellationToken expected = new(canceled: true);
                CancellationToken actual = default;
                Task task = new TaskCompletionSource<int>().Task;
                sut.OnCloseAsyncHandler = ct => { actual = ct; return task; };

                Task result = ((IStatefulUserServiceReplica)sut).OnCloseAsync(expected);

                Assert.Same(task, result);
                Assert.Equal(expected, actual);
            }

            [Fact(Explicit = true)] // TODO: SUT testability limitation. ServiceTelemetry is a static class; its events can't be intercepted without changes to StatefulServiceBase.
            public void RaisesStatefulServiceReplicaCloseEvent() =>
                throw new NotImplementedException(
                    "IStatefulUserServiceReplica.OnCloseAsync calls ServiceTelemetry.StatefulServiceReplicaCloseEvent. " +
                    "ServiceTelemetry is a static class so the invocation cannot be intercepted from a unit test " +
                    "without testability changes to the SUT.");
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

        public sealed class OnDataLossAsync_Protected_DefaultBehavior : StatefulServiceBaseTest
        {
            [Fact]
            public async Task ReturnsFalse() =>
                Assert.False(await sut.InvokeBaseOnDataLossAsync(default, TestContext.Current.CancellationToken));
        }

        public sealed class OnRestoreCompletedAsync_Protected_DefaultBehavior : StatefulServiceBaseTest
        {
            [Fact]
            public async Task ReturnsCompletedTask()
            {
                Task actual = sut.InvokeBaseOnRestoreCompletedAsync(TestContext.Current.CancellationToken);
                await actual;
                Assert.True(actual.IsCompletedSuccessfully);
            }
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
}
