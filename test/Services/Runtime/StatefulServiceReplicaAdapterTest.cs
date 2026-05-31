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

namespace Microsoft.ServiceFabric.Services.Runtime
{
    public abstract class StatefulServiceReplicaAdapterTest
    {
        readonly IStatefulServiceReplica sut;

        // Constructor parameters
        readonly StatefulServiceContext context = fuzzy.StatefulServiceContext();
        readonly Mock<IStatefulUserServiceReplica> userServiceReplica = new() { DefaultValue = DefaultValue.Mock };

        // Test fixture
        static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

        StatefulServiceReplicaAdapterTest() =>
            sut = new StatefulServiceReplicaAdapter(context, userServiceReplica.Object);

        IStateProviderReplica StateProvider => sut.Field<IStateProviderReplica>().Value;

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
            public void InvokesUserServiceOnAbort()
            {
                sut.Abort();
                userServiceReplica.Verify(_ => _.OnAbort(), Times.Once);
            }

            [Fact]
            public void AbortsStateProviderReplicaAndClearsIt()
            {
                IStateProviderReplica stateProvider = StateProvider;

                sut.Abort();

                Mock.Get(stateProvider).Verify(_ => _.Abort(), Times.Once);
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

            [Fact]
            public async Task ClosesExistingCommunicationListenersBeforeOpeningNew()
            {
                var existing = new Mock<ICommunicationListener>();
                sut.Field<IList<CommunicationListenerInfo>>().Set(new List<CommunicationListenerInfo>
                {
                    new(fuzzy.String(), existing.Object),
                });

                await sut.ChangeRoleAsync(ReplicaRole.None, cancellationToken);

                existing.Verify(_ => _.CloseAsync(cancellationToken), Times.Once);
                existing.Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task ForwardsToStateProviderReplica()
            {
                await sut.ChangeRoleAsync(ReplicaRole.None, cancellationToken);
                Mock.Get(StateProvider).Verify(_ => _.ChangeRoleAsync(ReplicaRole.None, cancellationToken), Times.Once);
                Mock.Get(StateProvider).Verify(_ => _.ChangeRoleAsync(It.IsAny<ReplicaRole>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task InvokesUserServiceOnChangeRoleAsyncAfterStateProvider()
            {
                int order = 0;
                int stateProviderOrder = 0;
                int userOrder = 0;
                Mock.Get(StateProvider)
                    .Setup(_ => _.ChangeRoleAsync(ReplicaRole.None, cancellationToken))
                    .Callback(() => stateProviderOrder = ++order)
                    .Returns(Task.CompletedTask);
                userServiceReplica
                    .Setup(_ => _.OnChangeRoleAsync(ReplicaRole.None, cancellationToken))
                    .Callback(() => userOrder = ++order)
                    .Returns(Task.CompletedTask);

                await sut.ChangeRoleAsync(ReplicaRole.None, cancellationToken);

                Assert.Equal(1, stateProviderOrder);
                Assert.Equal(2, userOrder);
            }

            [Fact]
            public async Task ReturnsEndpointCollectionToString()
            {
                string actual = await sut.ChangeRoleAsync(ReplicaRole.None, cancellationToken);
                Assert.Equal(sut.Field<ServiceEndpointCollection>().Value.ToString(), actual);
            }

            [Fact]
            public async Task OpensCommunicationListenersAndReturnsTheirEndpointsWhenNewRoleIsPrimary()
            {
                string name1 = fuzzy.String();
                string name2 = fuzzy.String();
                string address1 = fuzzy.String();
                string address2 = fuzzy.String();
                var listener1 = new Mock<ICommunicationListener>();
                var listener2 = new Mock<ICommunicationListener>();
                listener1.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(address1);
                listener2.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(address2);
                userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners()).Returns(new[]
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
                userServiceReplica.VerifySet(_ => _.Addresses = It.Is<IReadOnlyDictionary<string, string>>(
                    d => d.Count == 2 && d[name1] == address1 && d[name2] == address2));

                // Subsequent non-Primary ChangeRoleAsync closes the opened listeners, indirectly verifying storage.
                await sut.ChangeRoleAsync(ReplicaRole.None, cancellationToken);
                listener1.Verify(_ => _.CloseAsync(cancellationToken), Times.Once);
                listener1.Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
                listener2.Verify(_ => _.CloseAsync(cancellationToken), Times.Once);
                listener2.Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task UpdatesUserServiceReplicaAddressesFromOpenedListenersWhenNewRoleIsPrimary()
            {
                string name = fuzzy.String();
                string address = fuzzy.String();
                var listener = new Mock<ICommunicationListener>();
                listener.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(address);

                userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners())
                    .Returns(new[] { new ServiceReplicaListener(_ => listener.Object, name) });
                sut.Field<Func<ServiceReplicaListener, StatefulServiceContext, CommunicationListenerInfo>>()
                    .Set((entry, _) => new CommunicationListenerInfo(entry.Name, listener.Object));

                await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

                userServiceReplica.VerifySet(
                    _ => _.Addresses = It.Is<IReadOnlyDictionary<string, string>>(d => d.ContainsKey(name) && d[name].Contains(address)));
            }

            [Fact]
            public async Task SchedulesExecuteRunAsyncTaskWhenNewRoleIsPrimary()
            {
                await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

                Assert.NotNull(sut.Field<Task>().Value);
                Assert.NotNull(sut.Field<CancellationTokenSource>().Value);
            }

            [Fact]
            public async Task CancelsAndClearsRunAsyncTaskAndCancellationTokenSourceWhenNewRoleIsNotPrimary()
            {
                var existingCts = new CancellationTokenSource();
                sut.Field<CancellationTokenSource>().Set(existingCts);
                sut.Field<Task>().Set(Task.CompletedTask);

                await sut.ChangeRoleAsync(ReplicaRole.ActiveSecondary, cancellationToken);

                Assert.True(existingCts.IsCancellationRequested);
                Assert.Null(sut.Field<CancellationTokenSource>().Value);
                Assert.Null(sut.Field<Task>().Value);
            }

            [Fact]
            public async Task UpdatesUserServiceReplicaAddressesFromOpenedListenersWhenNewRoleIsActiveSecondary()
            {
                string name = fuzzy.String();
                string address = fuzzy.String();
                var listener = new Mock<ICommunicationListener>();
                listener.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(address);
                var entry = new ServiceReplicaListener(_ => listener.Object, name, listenOnSecondary: true);
                userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners()).Returns(new[] { entry });
                sut.Field<Func<ServiceReplicaListener, StatefulServiceContext, CommunicationListenerInfo>>()
                    .Set((e, _) => new CommunicationListenerInfo(e.Name, listener.Object));

                await sut.ChangeRoleAsync(ReplicaRole.ActiveSecondary, cancellationToken);

                userServiceReplica.VerifySet(
                    _ => _.Addresses = It.Is<IReadOnlyDictionary<string, string>>(d => d.ContainsKey(name) && d[name].Contains(address)));
            }

            [Fact]
            public async Task DefaultsToServiceReplicaListenerInstantiateForCreatingCommunicationListeners()
            {
                var listener = new Mock<ICommunicationListener>();
                var entry = new ServiceReplicaListener(_ => listener.Object, fuzzy.String());
                userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners()).Returns(new[] { entry });

                await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

                listener.Verify(_ => _.OpenAsync(cancellationToken), Times.Once);
                listener.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task OpensListenersThatListenOnSecondaryWhenNewRoleIsActiveSecondary()
            {
                var listener = new Mock<ICommunicationListener>();
                listener.Setup(_ => _.OpenAsync(cancellationToken)).ReturnsAsync(fuzzy.String());
                var entry = new ServiceReplicaListener(_ => listener.Object, fuzzy.String(), listenOnSecondary: true);
                userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners()).Returns(new[] { entry });
                sut.Field<Func<ServiceReplicaListener, StatefulServiceContext, CommunicationListenerInfo>>()
                    .Set((e, _) => new CommunicationListenerInfo(e.Name, listener.Object));

                await sut.ChangeRoleAsync(ReplicaRole.ActiveSecondary, cancellationToken);

                listener.Verify(_ => _.OpenAsync(cancellationToken), Times.Once);
                listener.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task SkipsListenersThatDoNotListenOnSecondaryWhenNewRoleIsActiveSecondary()
            {
                var listener = new Mock<ICommunicationListener>();
                var entry = new ServiceReplicaListener(_ => listener.Object, fuzzy.String(), listenOnSecondary: false);
                userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners()).Returns(new[] { entry });

                await sut.ChangeRoleAsync(ReplicaRole.ActiveSecondary, cancellationToken);

                listener.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Never);
                Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
            }

            [Fact]
            public async Task DoesNotOpenCommunicationListenersWhenNewRoleIsNone()
            {
                userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners())
                    .Returns(new[] { fuzzy.ServiceReplicaListener() });

                await sut.ChangeRoleAsync(ReplicaRole.None, cancellationToken);

                Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
            }

            [Fact]
            public async Task SkipsNullReplicaListeners()
            {
                userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners())
                    .Returns(new ServiceReplicaListener[] { null });

                await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

                Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
            }

            [Fact]
            public async Task SkipsListenersWhenCreateCommunicationListenerReturnsNull()
            {
                userServiceReplica.Setup(_ => _.CreateServiceReplicaListeners())
                    .Returns(new[] { fuzzy.ServiceReplicaListener() });
                sut.Field<Func<ServiceReplicaListener, StatefulServiceContext, CommunicationListenerInfo>>()
                    .Set((_, _) => null);

                await sut.ChangeRoleAsync(ReplicaRole.Primary, cancellationToken);

                Assert.Null(sut.Field<IList<CommunicationListenerInfo>>().Value);
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
                userServiceReplica
                    .Setup(_ => _.OnCloseAsync(cancellationToken))
                    .Callback(() => userOrder = ++order)
                    .Returns(Task.CompletedTask);
                Mock.Get(StateProvider)
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
            public async Task ClosesStateProviderReplicaAndClearsIt()
            {
                IStateProviderReplica stateProvider = StateProvider;

                await sut.CloseAsync(cancellationToken);

                Mock.Get(stateProvider).Verify(_ => _.CloseAsync(cancellationToken), Times.Once);
                Mock.Get(stateProvider).Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
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
                Assert.Equal(nameof(context), exception.ParamName);
            }

            [Fact]
            public void ThrowsArgumentNullExceptionWhenUserServiceReplicaIsNull()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => new StatefulServiceReplicaAdapter(context, null));
                Assert.Equal(nameof(userServiceReplica), exception.ParamName);
            }

            [Fact]
            public void SetsUserServiceReplicaAddressesToEmptyReadOnlyDictionary() =>
                userServiceReplica.VerifySet(
                    _ => _.Addresses = It.Is<IReadOnlyDictionary<string, string>>(d => d.Count == 0),
                    Times.Once());

            [Fact]
            public void InvokesCreateStateProviderReplicaOnce() =>
                userServiceReplica.Verify(_ => _.CreateStateProviderReplica(), Times.Once());

            [Fact]
            public void StoresStateProviderReplicaCreatedByUserServiceReplica() =>
                Assert.Same(userServiceReplica.Object.CreateStateProviderReplica(), StateProvider);
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
                stateProvider.As<IInternalStatefulServiceReplica>().Setup(_ => _.GetStatus()).Returns(expected);
                base.sut.Field<IStateProviderReplica>().Set(stateProvider.Object);

                Assert.Same(expected, sut.GetStatus());
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
                Mock.Get(StateProvider).Verify(_ => _.Initialize(parameters), Times.Once);
                Mock.Get(StateProvider).Verify(_ => _.Initialize(It.IsAny<StatefulServiceInitializationParameters>()), Times.Once);
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
                await sut.OpenAsync(openMode, partition, cancellationToken);
                Assert.Same(partition, sut.Field<IStatefulServicePartition>().Value);
            }

            [Fact]
            public async Task SetsUserServiceReplicaPartition()
            {
                await sut.OpenAsync(openMode, partition, cancellationToken);
                userServiceReplica.VerifySet(_ => _.Partition = partition);
            }

            [Fact]
            public async Task ReturnsReplicatorFromStateProviderOpenAsync()
            {
                IReplicator expected = Mock.Of<IReplicator>();
                Mock.Get(StateProvider).Setup(_ => _.OpenAsync(openMode, partition, cancellationToken)).ReturnsAsync(expected);

                IReplicator actual = await sut.OpenAsync(openMode, partition, cancellationToken);

                Assert.Same(expected, actual);
            }

            [Fact]
            public async Task InvokesUserServiceOnOpenAsync()
            {
                await sut.OpenAsync(openMode, partition, cancellationToken);
                userServiceReplica.Verify(_ => _.OnOpenAsync(openMode, cancellationToken), Times.Once);
                userServiceReplica.Verify(_ => _.OnOpenAsync(It.IsAny<ReplicaOpenMode>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task InvokesUserServiceOnOpenAsyncAfterStateProviderOpenAsync()
            {
                int order = 0;
                int stateProviderOrder = 0;
                int userOrder = 0;
                Mock.Get(StateProvider)
                    .Setup(_ => _.OpenAsync(openMode, partition, cancellationToken))
                    .Callback(() => stateProviderOrder = ++order)
                    .ReturnsAsync(Mock.Of<IReplicator>());
                userServiceReplica
                    .Setup(_ => _.OnOpenAsync(openMode, cancellationToken))
                    .Callback(() => userOrder = ++order)
                    .Returns(Task.CompletedTask);

                await sut.OpenAsync(openMode, partition, cancellationToken);

                Assert.Equal(1, stateProviderOrder);
                Assert.Equal(2, userOrder);
            }

            [Fact]
            public async Task ClosesStateProviderAndRethrowsWhenOnOpenAsyncThrows()
            {
                var expected = new InvalidOperationException(fuzzy.String());
                userServiceReplica.Setup(_ => _.OnOpenAsync(openMode, cancellationToken)).ThrowsAsync(expected);
                IStateProviderReplica stateProvider = StateProvider;

                var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => sut.OpenAsync(openMode, partition, cancellationToken));

                Assert.Same(expected, actual);
                Mock.Get(stateProvider).Verify(_ => _.CloseAsync(cancellationToken), Times.Once);
                Mock.Get(stateProvider).Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
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
}
