// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using Fuzzy;
using Inspector;
using Microsoft.ServiceFabric.Data;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Runtime;

public abstract class StatefulServiceReplicaFactoryTest
{
    readonly StatefulServiceReplicaFactory sut;

    // Constructor parameters
    readonly RuntimeContext runtimeContext = new();
    readonly Func<StatefulServiceContext, StatefulServiceBase> serviceFactory;

    // Test fixture
    readonly NodeContext nodeContext = fuzzy.NodeContext();
    readonly ICodePackageActivationContext codePackageContext = fuzzy.ICodePackageActivationContext();
    readonly StatefulServiceBase service;

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    StatefulServiceReplicaFactoryTest()
    {
        runtimeContext.Property<NodeContext>().Set(nodeContext);
        runtimeContext.Property<ICodePackageActivationContext>().Set(codePackageContext);

        service = new TestStatefulService(fuzzy.StatefulServiceContext(), Mock.Of<IReliableStateManagerReplica>());
        serviceFactory = _ => service;

        sut = new StatefulServiceReplicaFactory(runtimeContext, serviceFactory);
    }

    public sealed class Constructor : StatefulServiceReplicaFactoryTest
    {
        [Fact(Explicit = true)] // TODO: SUT bug. Constructor doesn't validate runtimeContext.
        public void ThrowsArgumentNullExceptionWhenRuntimeContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new StatefulServiceReplicaFactory(null, serviceFactory));
            Assert.Equal(nameof(runtimeContext), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Constructor doesn't validate serviceFactory.
        public void ThrowsArgumentNullExceptionWhenServiceFactoryIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new StatefulServiceReplicaFactory(runtimeContext, null));
            Assert.Equal(nameof(serviceFactory), exception.ParamName);
        }
    }

    public sealed class CreateReplica : StatefulServiceReplicaFactoryTest
    {
        // Method parameters
        readonly string serviceTypeName = fuzzy.String();
        readonly Uri serviceName = fuzzy.Uri();
        readonly byte[] initializationData = fuzzy.Array(fuzzy.Byte);
        readonly Guid partitionId = Guid.NewGuid();
        readonly long replicaId = fuzzy.Int64();

        [Fact]
        public void InvokesServiceFactoryWithStatefulServiceContextBuiltFromArgumentsAndRuntimeContext()
        {
            StatefulServiceContext captured = null;
            int callCount = 0;
            var sut = (IStatefulServiceFactory)new StatefulServiceReplicaFactory(runtimeContext, ctx =>
            {
                callCount++;
                captured = ctx;
                return service;
            });

            _ = sut.CreateReplica(serviceTypeName, serviceName, initializationData, partitionId, replicaId);

            Assert.Equal(1, callCount);
            Assert.NotNull(captured);
            Assert.Same(nodeContext, captured.NodeContext);
            Assert.Same(codePackageContext, captured.CodePackageActivationContext);
            Assert.Equal(serviceTypeName, captured.ServiceTypeName);
            Assert.Equal(serviceName, captured.ServiceName);
            Assert.Same(initializationData, captured.InitializationData);
            Assert.Equal(partitionId, captured.PartitionId);
            Assert.Equal(replicaId, captured.ReplicaId);
        }

        [Fact]
        public void ReturnsStatefulServiceReplicaAdapter()
        {
            int callCount = 0;
            var sut = (IStatefulServiceFactory)new StatefulServiceReplicaFactory(runtimeContext, _ =>
            {
                callCount++;
                return service;
            });

            var actual = sut.CreateReplica(serviceTypeName, serviceName, initializationData, partitionId, replicaId);

            Assert.Equal(1, callCount);
            _ = (StatefulServiceReplicaAdapter)actual;
        }
    }

    public sealed class Dispose : StatefulServiceReplicaFactoryTest
    {
        [Fact]
        public void DisposesRuntimeContext()
        {
            sut.Dispose();
            Mock.Get(codePackageContext).Verify(_ => _.Dispose(), Times.Once);
        }

        [Fact]
        public void DoesNotThrowWhenRuntimeContextIsNull()
        {
            var sut = new StatefulServiceReplicaFactory(null, serviceFactory);
            sut.Dispose();
        }
    }

    sealed class TestStatefulService : StatefulServiceBase
    {
        internal TestStatefulService(StatefulServiceContext serviceContext, IStateProviderReplica stateProviderReplica)
            : base(serviceContext, stateProviderReplica) { }
    }
}
