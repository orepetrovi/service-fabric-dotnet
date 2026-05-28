// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using Inspector;
using Microsoft.ServiceFabric.Actors.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Actors.Remoting;

public abstract class ActorRemotingProviderAttributeTest
{
    public sealed class Constructor : ActorRemotingProviderAttributeTest
    {
        readonly ActorRemotingProviderAttribute sut = new TestActorRemotingProviderAttribute();

        [Fact]
        public void InitializesRemotingClientVersionToV2_1() =>
            Assert.Equal(RemotingClientVersion.V2_1, sut.RemotingClientVersion);

        [Fact]
        public void InitializesRemotingListenerVersionToV2_1() =>
            Assert.Equal(RemotingListenerVersion.V2_1, sut.RemotingListenerVersion);
    }

    public sealed class GetProvider : ActorRemotingProviderAttributeTest, IDisposable
    {
        readonly Assembly assemblyWithoutRemotingProviderAttribute = MockAssembly();
        readonly Assembly assemblyWithRemotingProviderAttribute;
        readonly ActorRemotingProviderAttribute expectedRemotingProvider = new FabricTransportActorRemotingProviderAttribute();

        public GetProvider() =>
            assemblyWithRemotingProviderAttribute = MockAssembly(expectedRemotingProvider);

        void IDisposable.Dispose() =>
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(Assembly.GetEntryAssembly());

        [Fact]
        public void ReturnsRemotingProviderAttributeOfEntryAssemblyWhenTypesIsNull()
        {
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(assemblyWithRemotingProviderAttribute);

            ActorRemotingProviderAttribute provider = ActorRemotingProviderAttribute.GetProvider();

            Assert.Same(expectedRemotingProvider, provider);
        }

        [Fact]
        public void ReturnsRemotingProviderAttributeOfTypeAssembly()
        {
            Type type = MockType(assemblyWithRemotingProviderAttribute);
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(null);

            ActorRemotingProviderAttribute provider = ActorRemotingProviderAttribute.GetProvider(new[] { type });

            Assert.Same(expectedRemotingProvider, provider);
        }

        [Fact]
        public void ReturnsRemotingProviderAttributeOfLaterTypeWhenEarlierTypeAssemblyHasNoAttribute()
        {
            Type typeWithoutAttr = MockType(assemblyWithoutRemotingProviderAttribute);
            Type typeWithAttr = MockType(assemblyWithRemotingProviderAttribute);
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(null);

            ActorRemotingProviderAttribute provider = ActorRemotingProviderAttribute.GetProvider(new[] { typeWithoutAttr, typeWithAttr });

            Assert.Same(expectedRemotingProvider, provider);
        }

        [Fact]
        public void ReturnsRemotingProviderAttributeOfEarlierTypeWhenMultipleTypeAssembliesHaveAttributes()
        {
            Type earlierType = MockType(assemblyWithRemotingProviderAttribute);
            var otherProvider = new FabricTransportActorRemotingProviderAttribute();
            Type laterType = MockType(MockAssembly(otherProvider));
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(null);

            ActorRemotingProviderAttribute provider = ActorRemotingProviderAttribute.GetProvider(new[] { earlierType, laterType });

            Assert.Same(expectedRemotingProvider, provider);
        }

        [Fact]
        public void ReturnsRemotingProviderAttributeOfTypeAssemblyWhenEntryAssemblyAlsoHasAttribute()
        {
            Type type = MockType(assemblyWithRemotingProviderAttribute);
            var entryProvider = new FabricTransportActorRemotingProviderAttribute();
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(MockAssembly(entryProvider));

            ActorRemotingProviderAttribute provider = ActorRemotingProviderAttribute.GetProvider(new[] { type });

            Assert.Same(expectedRemotingProvider, provider);
        }

        [Fact]
        public void ReturnsRemotingProviderAttributeOfEntryAssemblyWhenNoTypeAssemblyHasAttribute()
        {
            Type type = MockType(assemblyWithoutRemotingProviderAttribute);
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(assemblyWithRemotingProviderAttribute);

            ActorRemotingProviderAttribute provider = ActorRemotingProviderAttribute.GetProvider(new[] { type });

            Assert.Same(expectedRemotingProvider, provider);
        }

        [Fact]
        public void ReturnsDefaultFabricTransportActorRemotingProviderWhenTypeHasNoAssemblyProviderAttribute()
        {
            Type type = MockType(assemblyWithoutRemotingProviderAttribute);
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(null);

            ActorRemotingProviderAttribute result = ActorRemotingProviderAttribute.GetProvider(new[] { type });

            Assert.IsType<FabricTransportActorRemotingProviderAttribute>(result);
        }

        [Fact]
        public void ReturnsDefaultFabricTransportActorRemotingProviderWhenNeitherTypeAssemblyNorEntryAssemblyHasAttribute()
        {
            Type type = MockType(assemblyWithoutRemotingProviderAttribute);
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(assemblyWithoutRemotingProviderAttribute);

            ActorRemotingProviderAttribute result = ActorRemotingProviderAttribute.GetProvider(new[] { type });

            Assert.IsType<FabricTransportActorRemotingProviderAttribute>(result);
        }

        [Fact]
        public void ReturnsDefaultFabricTransportActorRemotingProviderWhenTypesIsNullAndEntryAssemblyIsNull()
        {
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(null);

            ActorRemotingProviderAttribute result = ActorRemotingProviderAttribute.GetProvider();

            Assert.IsType<FabricTransportActorRemotingProviderAttribute>(result);
        }
    }

    public sealed class RemotingClientVersion_ : ActorRemotingProviderAttributeTest
    {
        readonly ActorRemotingProviderAttribute sut = new TestActorRemotingProviderAttribute();

        [Theory]
        [InlineData(RemotingClientVersion.V2)]
        [InlineData(RemotingClientVersion.V2_1)]
        public void SetsValue(RemotingClientVersion value)
        {
            sut.RemotingClientVersion = value;
            Assert.Equal(value, sut.RemotingClientVersion);
        }
    }

    public sealed class RemotingListenerVersion_ : ActorRemotingProviderAttributeTest
    {
        readonly ActorRemotingProviderAttribute sut = new TestActorRemotingProviderAttribute();

        [Theory]
        [InlineData(RemotingListenerVersion.V2)]
        [InlineData(RemotingListenerVersion.V2_1)]
        public void SetsValue(RemotingListenerVersion value)
        {
            sut.RemotingListenerVersion = value;
            Assert.Equal(value, sut.RemotingListenerVersion);
        }
    }

    static Assembly MockAssembly(ActorRemotingProviderAttribute provider = null)
    {
        var assembly = new Mock<TestAssembly>();
        Attribute[] attributes = provider == null ? [] : new[] { provider };
        assembly.Setup(_ => _.GetCustomAttributes(typeof(ActorRemotingProviderAttribute), true)).Returns(attributes);
        return assembly.Object;
    }

    static Type MockType(Assembly assembly)
    {
        var type = new Mock<Type>();
        type.Setup(_ => _.Assembly).Returns(assembly);
#if NETFRAMEWORK
        var reflectableType = type.As<IReflectableType>();
        reflectableType.Setup(_ => _.GetTypeInfo()).Returns(MockTypeInfo(assembly));
#endif
        return type.Object;
    }

#if NETFRAMEWORK
    static TypeInfo MockTypeInfo(Assembly assembly)
    {
        var typeInfo = new Mock<TypeDelegator>();
        typeInfo.Setup(_ => _.Assembly).Returns(assembly);
        return typeInfo.Object;
    }
#endif

    // Make Assembly concrete to enable mocking on NetFx
    public class TestAssembly : Assembly { }

    sealed class TestActorRemotingProviderAttribute : ActorRemotingProviderAttribute
    {
        public override Dictionary<string, Func<ActorService, IServiceRemotingListener>> CreateServiceRemotingListeners() =>
            throw new NotImplementedException();

        public override IServiceRemotingClientFactory CreateServiceRemotingClientFactory(IServiceRemotingCallbackMessageHandler callbackMessageHandler) =>
            throw new NotImplementedException();
    }
}
