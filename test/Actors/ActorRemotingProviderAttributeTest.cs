using System;
using System.Collections.Generic;
using System.Reflection;
using Inspector;
using Microsoft.ServiceFabric.Actors.Remoting;
using Microsoft.ServiceFabric.Actors.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Actors.Tests;

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
        readonly Assembly mockAssemblyWithoutRemotingProviderAttribute = MockAssembly();
        readonly Assembly mockAssemblyWithRemotingProviderAttribute;
        readonly ActorRemotingProviderAttribute expectedRemotingProvider = new FabricTransportActorRemotingProviderAttribute();

        public GetProvider() =>
            mockAssemblyWithRemotingProviderAttribute = MockAssembly(expectedRemotingProvider);

        void IDisposable.Dispose() =>
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(Assembly.GetEntryAssembly());

        [Fact]
        public void ReturnsRemotingProviderAttributeOfEntryAssemblyWhenTypesIsNull()
        {
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(mockAssemblyWithRemotingProviderAttribute);

            ActorRemotingProviderAttribute provider = ActorRemotingProviderAttribute.GetProvider();

            Assert.Same(expectedRemotingProvider, provider);
        }

        [Fact]
        public void ReturnsRemotingProviderAttributeOfTypeAssembly()
        {
            Type type = MockType(mockAssemblyWithRemotingProviderAttribute);
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(null);

            ActorRemotingProviderAttribute provider = ActorRemotingProviderAttribute.GetProvider(new[] { type });

            Assert.Same(expectedRemotingProvider, provider);
        }

        [Fact]
        public void ReturnsRemotingProviderAttributeOfLaterTypeWhenEarlierTypeAssemblyHasNoAttribute()
        {
            Type typeWithoutAttr = MockType(mockAssemblyWithoutRemotingProviderAttribute);
            Type typeWithAttr = MockType(mockAssemblyWithRemotingProviderAttribute);
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(null);

            ActorRemotingProviderAttribute provider = ActorRemotingProviderAttribute.GetProvider(new[] { typeWithoutAttr, typeWithAttr });

            Assert.Same(expectedRemotingProvider, provider);
        }

        [Fact]
        public void ReturnsRemotingProviderAttributeOfEntryAssemblyWhenNoTypeAssemblyHasAttribute()
        {
            Type type = MockType(mockAssemblyWithoutRemotingProviderAttribute);
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(mockAssemblyWithRemotingProviderAttribute);

            ActorRemotingProviderAttribute provider = ActorRemotingProviderAttribute.GetProvider(new[] { type });

            Assert.Same(expectedRemotingProvider, provider);
        }

        [Fact]
        public void ReturnsDefaultFabricTransportActorRemotingProviderWhenTypeHasNoAssemblyProviderAttribute()
        {
            Type type = MockType(mockAssemblyWithoutRemotingProviderAttribute);
            typeof(ActorRemotingProviderAttribute).Field<Assembly>().Set(null);

            var result = ActorRemotingProviderAttribute.GetProvider(new[] { type });

            var expected = new FabricTransportActorRemotingProviderAttribute();
            var actual = Assert.IsType<FabricTransportActorRemotingProviderAttribute>(result);
            Assert.Equal(expected.RemotingClientVersion, actual.RemotingClientVersion);
            Assert.Equal(expected.RemotingListenerVersion, actual.RemotingListenerVersion);
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

    public sealed class StaticConstructor : ActorRemotingProviderAttributeTest
    {
        [Fact]
        public void InitializesEntryAssembly() =>
            Assert.Same(Assembly.GetEntryAssembly(), typeof(ActorRemotingProviderAttribute).Field<Assembly>().Value);
    }

    static Assembly MockAssembly(ActorRemotingProviderAttribute provider = null)
    {
        var assembly = new Mock<TestAssembly>();
        Attribute[] attributes = provider == null ? new Attribute[0] : new[] { provider };
        assembly.Setup(_ => _.GetCustomAttributes(typeof(ActorRemotingProviderAttribute), It.IsAny<bool>())).Returns(attributes);
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
