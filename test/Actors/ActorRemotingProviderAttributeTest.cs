using System;
using System.Reflection;
using Inspector;
using Microsoft.ServiceFabric.Actors.Remoting;
using Microsoft.ServiceFabric.Actors.Remoting.FabricTransport;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Actors.Tests;

public abstract class ActorRemotingProviderAttributeTest
{
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
}
