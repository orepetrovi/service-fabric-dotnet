// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using Fuzzy;
using Microsoft.ServiceFabric.Data;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Runtime;

public abstract class StatefulServiceTest
{
    readonly StatefulService sut;

    // Constructor parameters
    readonly StatefulServiceContext serviceContext = fuzzy.StatefulServiceContext();
    readonly IReliableStateManagerReplica reliableStateManagerReplica = Mock.Of<IReliableStateManagerReplica>();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    StatefulServiceTest() =>
        sut = new TestStatefulService(serviceContext, reliableStateManagerReplica);

    public sealed class Constructor_StatefulServiceContext : StatefulServiceTest
    {
        [Fact(Explicit = true)] // TODO: SUT testability limitation. ReliableStateManager loads native Microsoft.ServiceFabric.Data.Impl assembly via FabricRuntime.LoadAssembly.
        public void InitializesStateManagerWithDefaultReliableStateManager() =>
            throw new NotImplementedException(
                "StatefulService(StatefulServiceContext) chains to itself with new ReliableStateManager(serviceContext), " +
                "which calls FabricRuntime.LoadAssembly(\"Microsoft.ServiceFabric.Data.Impl\"). That native assembly is " +
                "not available in the test environment, so the default-state-manager construction path cannot be covered " +
                "without testability changes to the SUT.");
    }

    public sealed class Constructor_StatefulServiceContext_IReliableStateManagerReplica : StatefulServiceTest
    {
        [Fact]
        public void InitializesStateManagerWithReliableStateManagerReplica() =>
            Assert.Same(reliableStateManagerReplica, sut.StateManager);

        [Fact]
        public void ThrowsArgumentNullExceptionWhenServiceContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TestStatefulService(null, reliableStateManagerReplica));
            Assert.Equal(nameof(serviceContext), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenReliableStateManagerReplicaIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TestStatefulService(serviceContext, null));
            Assert.Equal("stateProviderReplica", exception.ParamName);
        }
    }

    sealed class TestStatefulService : StatefulService
    {
        public TestStatefulService(StatefulServiceContext serviceContext, IReliableStateManagerReplica reliableStateManagerReplica)
            : base(serviceContext, reliableStateManagerReplica) { }
    }
}
