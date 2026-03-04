// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using Fuzzy;
using Inspector;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Runtime;

public abstract class StatelessServiceInstanceFactoryTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    // Constructor parameters
    readonly RuntimeContext runtimeContext;
    readonly Func<StatelessServiceContext, StatelessService> serviceFactory;

    protected StatelessServiceInstanceFactoryTest()
    {
        runtimeContext = Mock.Of<RuntimeContext>(ctx => 
            ctx.NodeContext == fuzzy.NodeContext() &&
            ctx.CodePackageContext == fuzzy.ICodePackageActivationContext());
            
        serviceFactory = Mock.Of<Func<StatelessServiceContext, StatelessService>>();
    }

    StatelessServiceInstanceFactory CreateSut() =>
        new StatelessServiceInstanceFactory(runtimeContext, serviceFactory);

    public sealed class Constructor : StatelessServiceInstanceFactoryTest
    {
        [Fact]
        public void ThrowsArgumentNullExceptionWhenRuntimeContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StatelessServiceInstanceFactory(null, serviceFactory));
            Assert.Equal(nameof(runtimeContext), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenServiceFactoryIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StatelessServiceInstanceFactory(runtimeContext, null));
            Assert.Equal(nameof(serviceFactory), exception.ParamName);
        }
    }

    public sealed class CreateInstance : StatelessServiceInstanceFactoryTest
    {
        readonly string serviceTypeName = fuzzy.String();
        readonly Uri serviceName = fuzzy.Uri();
        readonly byte[] initializationData = fuzzy.Array(fuzzy.Byte);
        readonly Guid partitionId = Guid.NewGuid();
        readonly long instanceId = fuzzy.Int64();

        readonly Mock<StatelessService> mockService;
        readonly StatelessServiceContext serviceContext;

        public CreateInstance()
        {
            serviceContext = fuzzy.StatelessServiceContext();
            mockService = new Mock<StatelessService>(serviceContext) { DefaultValue = DefaultValue.Mock };

            _ = Mock.Get(serviceFactory)
                .Setup(f => f(It.IsAny<StatelessServiceContext>()))
                .Returns(mockService.Object);
        }

        [Fact]
        public void InvokesServiceFactory()
        {
            IStatelessServiceFactory sut = CreateSut();

            _ = sut.CreateInstance(serviceTypeName, serviceName, initializationData, partitionId, instanceId);

            Mock.Get(serviceFactory).Verify(f => f(It.Is<StatelessServiceContext>(ctx =>
                ctx.NodeContext == runtimeContext.NodeContext &&
                ctx.CodePackageActivationContext == runtimeContext.CodePackageContext &&
                ctx.ServiceTypeName == serviceTypeName &&
                ctx.ServiceName == serviceName &&
                ctx.InitializationData == initializationData &&
                ctx.PartitionId == partitionId &&
                ctx.InstanceId == instanceId)));
        }

        [Fact]
        public void ReturnsStatelessServiceInstance()
        {
            IStatelessServiceFactory sut = CreateSut();

            IStatelessServiceInstance result = sut.CreateInstance(serviceTypeName, serviceName, initializationData, partitionId, instanceId);

            var adapter = Assert.IsType<StatelessServiceInstanceAdapter>(result);
            Assert.Same(mockService.Object, adapter.Field<IStatelessUserServiceInstance>().Value);
            Assert.Same(mockService.Object.Context, adapter.Field<StatelessServiceContext>().Value);
        }

        [Fact]
        public void ThrowsInvalidOperationExceptionWhenServiceFactoryReturnsNull()
        {
            _ = Mock.Get(serviceFactory)
                .Setup(f => f(It.IsAny<StatelessServiceContext>()))
                .Returns((StatelessService)null);

            IStatelessServiceFactory sut = CreateSut();

            _ = Assert.Throws<InvalidOperationException>(() =>
                sut.CreateInstance(serviceTypeName, serviceName, initializationData, partitionId, instanceId));
        }
    }

    public sealed class Dispose : StatelessServiceInstanceFactoryTest
    {
        [Fact]
        public void DisposesRuntimeContext()
        {
            var runtimeContextMock = Mock.Get(runtimeContext);

            var sut = CreateSut();
            sut.Dispose();

            runtimeContextMock.Verify(c => c.Dispose(), Times.Once);
        }
    }
}
