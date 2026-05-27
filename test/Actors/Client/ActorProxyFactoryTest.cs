// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Fuzzy;
using Inspector;
using Microsoft.ServiceFabric.Actors.Remoting.V2.Client;
using Microsoft.ServiceFabric.Actors.Tests;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Actors.Client;

/// <summary>
/// Public actor interface used to exercise <see cref="ActorProxyFactory"/>. The dynamic assembly produced
/// by <c>ActorCodeBuilder</c> cannot access <c>internal</c> interfaces defined in this test assembly,
/// so the interface must be <c>public</c>.
/// </summary>
public interface IFactoryTestActor : IActor
{
    Task TestMethod();
}

public interface IFactoryTestActorService : IActorService
{
}

public abstract class ActorProxyFactoryTest
{
    readonly ActorProxyFactory sut;

    // Constructor parameters
    readonly Func<IServiceRemotingCallbackMessageHandler, IServiceRemotingClientFactory> createServiceRemotingClientFactory;
    readonly OperationRetrySettings retrySettings = new();

    readonly IServiceRemotingClientFactory factory = new Mock<IServiceRemotingClientFactory> { DefaultValue = DefaultValue.Mock }.Object;

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ActorProxyFactoryTest()
    {
        createServiceRemotingClientFactory = _ => factory;
        sut = new ActorProxyFactory(createServiceRemotingClientFactory, retrySettings);
    }

    public sealed class Constructor : ActorProxyFactoryTest
    {
        [Fact]
        public void StoresRetrySettingsAndLeavesProxyFactoryNull()
        {
            var sut = new ActorProxyFactory(retrySettings);

            Assert.Same(retrySettings, sut.Field<OperationRetrySettings>().Value);
            Assert.Null(sut.Field<Remoting.V2.Client.ActorProxyFactory>().Value);
        }

        [Fact]
        public void StoresNullRetrySettingsByDefault()
        {
            var sut = new ActorProxyFactory();

            Assert.Null(sut.Field<OperationRetrySettings>().Value);
            Assert.Null(sut.Field<Remoting.V2.Client.ActorProxyFactory>().Value);
        }
    }

    public sealed class Constructor_Func : ActorProxyFactoryTest
    {
        [Fact]
        public void CreatesProxyFactoryWithGivenFuncAndRetrySettings()
        {
            // The V2 ActorProxyFactory is internal, so inspect its private state to verify the V1 constructor
            // forwarded both parameters into it. The Func behavior is further exercised indirectly by the
            // CreateActorProxy tests, which require the Func to return a usable IServiceRemotingClientFactory.
            var v2 = sut.Field<Remoting.V2.Client.ActorProxyFactory>().Value;
            Assert.NotNull(v2);
            Assert.Same(
                createServiceRemotingClientFactory,
                v2.Field<Func<IServiceRemotingCallbackMessageHandler, IServiceRemotingClientFactory>>().Value);
            Assert.Same(retrySettings, v2.Field<OperationRetrySettings>().Value);
        }
    }

    public sealed class CreateActorProxy_ActorId_String_String_String : ActorProxyFactoryTest
    {
        // Method parameters
        readonly ActorId actorId = fuzzy.ActorId();
        readonly string applicationName = "fabric:/" + fuzzy.String().LettersOrDigits();
        readonly string serviceName = fuzzy.String().LettersOrDigits();
        readonly string listenerName = fuzzy.String();

        [Fact]
        public void ReturnsProxyWithActorIdAndServiceUriBuiltFromApplicationNameAndServiceName()
        {
            var expectedUri = new Uri($"{applicationName}/{serviceName}");

            var proxy = (IActorProxy)sut.CreateActorProxy<IFactoryTestActor>(actorId, applicationName, serviceName, listenerName);

            Assert.Same(actorId, proxy.ActorId);
            Assert.Equal(expectedUri, proxy.ActorServicePartitionClientV2.ServiceUri);
            Assert.Equal(listenerName, proxy.ActorServicePartitionClientV2.ListenerName);
        }

        [Fact]
        public void LazilyCreatesV2ProxyFactoryFromDefaultProviderAndForwardsRetrySettings()
        {
            // Exercises the lazy-init branch in GetOrSetProxyFactory reachable only when ActorProxyFactory is
            // constructed without a Func. proxyFactoryV2 is assigned before the V2 factory invokes the default
            // provider's CreateServiceRemotingClientFactory delegate, which requires the Service Fabric runtime
            // and is expected to fail in this unit test environment.
            var sut = new ActorProxyFactory(retrySettings);

            try
            {
                sut.CreateActorProxy<IFactoryTestActor>(actorId, applicationName, serviceName, listenerName);
            }
            catch
            {
            }

            var v2 = sut.Field<Remoting.V2.Client.ActorProxyFactory>().Value;
            Assert.NotNull(v2);
            Assert.Same(retrySettings, v2.Field<OperationRetrySettings>().Value);
        }

        [Fact]
        public void SubstitutesDefaultListenerNameWhenLazyInitOverrodeItAndCallerSuppliedNull()
        {
            // Exercises the override branch in OverrideListenerNameIfConditionMet: lazy init via the no-Func
            // constructor sets overrideListenerName=true and defaultListenerName from the default provider
            // (FabricTransportActorRemotingProviderAttribute => V2 => DefaultV2listenerName). The V2 factory
            // built by lazy init is wired to the default provider whose factory delegate requires the Service
            // Fabric runtime, so it is replaced with one bound to the mock client factory before creating the
            // proxy whose ListenerName is then observed.
            var sut = new ActorProxyFactory(retrySettings);
            try
            {
                sut.CreateActorProxy<IFactoryTestActor>(actorId, applicationName, serviceName, listenerName);
            }
            catch
            {
            }

            sut.Field<Remoting.V2.Client.ActorProxyFactory>()
                .Set(new Remoting.V2.Client.ActorProxyFactory(createServiceRemotingClientFactory, retrySettings));

            var proxy = (IActorProxy)sut.CreateActorProxy<IFactoryTestActor>(actorId, applicationName, serviceName, listenerName: null);

            Assert.Equal(ServiceRemotingProviderAttribute.DefaultV2listenerName, proxy.ActorServicePartitionClientV2.ListenerName);
        }
    }

    public sealed class CreateActorProxy_Uri_ActorId_String : ActorProxyFactoryTest
    {
        // Method parameters
        readonly Uri serviceUri = fuzzy.Uri();
        readonly ActorId actorId = fuzzy.ActorId();
        readonly string listenerName = fuzzy.String();

        [Fact]
        public void ReturnsProxyWithGivenServiceUriActorIdAndListenerName()
        {
            var proxy = (IActorProxy)sut.CreateActorProxy<IFactoryTestActor>(serviceUri, actorId, listenerName);

            Assert.Same(actorId, proxy.ActorId);
            Assert.Same(serviceUri, proxy.ActorServicePartitionClientV2.ServiceUri);
            Assert.Equal(listenerName, proxy.ActorServicePartitionClientV2.ListenerName);
            Assert.Same(factory, proxy.ActorServicePartitionClientV2.Factory);
        }
    }

    public sealed class CreateActorServiceProxy_Uri_ActorId_String : ActorProxyFactoryTest
    {
        // Method parameters
        readonly Uri serviceUri = fuzzy.Uri();
        readonly ActorId actorId = fuzzy.ActorId();
        readonly string listenerName = fuzzy.String();

        [Fact]
        public void ReturnsProxyWithPartitionKeyDerivedFromActorId()
        {
            var proxy = (IServiceProxy)sut.CreateActorServiceProxy<IFactoryTestActorService>(serviceUri, actorId, listenerName);

            Assert.Same(serviceUri, proxy.ServicePartitionClient2.ServiceUri);
            Assert.Equal(listenerName, proxy.ServicePartitionClient2.ListenerName);
            Assert.Equal(actorId.GetPartitionKey(), proxy.ServicePartitionClient2.PartitionKey.Value);
        }
    }

    public sealed class CreateActorServiceProxy_Uri_Int64_String : ActorProxyFactoryTest
    {
        // Method parameters
        readonly Uri serviceUri = fuzzy.Uri();
        readonly long partitionKey = fuzzy.Int64();
        readonly string listenerName = fuzzy.String();

        [Fact]
        public void ReturnsProxyWithGivenServiceUriListenerNameAndPartitionKey()
        {
            var proxy = (IServiceProxy)sut.CreateActorServiceProxy<IFactoryTestActorService>(serviceUri, partitionKey, listenerName);

            Assert.Same(serviceUri, proxy.ServicePartitionClient2.ServiceUri);
            Assert.Equal(listenerName, proxy.ServicePartitionClient2.ListenerName);
            Assert.Equal(partitionKey, proxy.ServicePartitionClient2.PartitionKey.Value);
        }
    }

    public sealed class CreateActorProxy_Type_Uri_ActorId_String : ActorProxyFactoryTest
    {
        // Method parameters
        readonly Type actorInterfaceType = typeof(IFactoryTestActor);
        readonly Uri serviceUri = fuzzy.Uri();
        readonly ActorId actorId = fuzzy.ActorId();
        readonly string listenerName = fuzzy.String();

        [Fact]
        public void ReturnsProxyWithGivenServiceUriActorIdAndListenerName()
        {
            var proxy = (IActorProxy)sut.CreateActorProxy(actorInterfaceType, serviceUri, actorId, listenerName);

            Assert.Same(actorId, proxy.ActorId);
            Assert.Same(serviceUri, proxy.ActorServicePartitionClientV2.ServiceUri);
            Assert.Equal(listenerName, proxy.ActorServicePartitionClientV2.ListenerName);
        }
    }

    public sealed class Dispose : ActorProxyFactoryTest
    {
        [Fact]
        public void DoesNothingWhenProxyFactoryWasNeverCreated()
        {
            var sut = new ActorProxyFactory();

            sut.Dispose();

            Assert.Null(sut.Field<Remoting.V2.Client.ActorProxyFactory>().Value);
        }

        [Fact]
        public void DelegatesToUnderlyingProxyFactoryWithoutThrowing()
        {
            // The V2 ActorProxyFactory.Dispose only disposes the remoting client factory when it is a
            // FabricTransportActorRemotingClientFactory. The mocked IServiceRemotingClientFactory used here
            // is not, so the call is a no-op against the mock. The test asserts that Dispose forwards the
            // call without throwing, which is the observable contract of the V1 wrapper.
            sut.Dispose();
        }
    }
}
