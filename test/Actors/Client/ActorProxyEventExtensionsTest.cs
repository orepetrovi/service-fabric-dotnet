// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Fuzzy;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Actors.Client;

public abstract class ActorProxyEventExtensionsTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    public sealed class SubscribeAsync_IActorEventPublisher_TEvent : ActorProxyEventExtensionsTest
    {
        readonly IActorEventPublisher actorProxy = new TestProxy();
        readonly IActorEvents subscriber = Mock.Of<IActorEvents>();

        [Fact]
        public async Task ThrowsArgumentExceptionWhenActorProxyIsNotActorProxy()
        {
            IActorEventPublisher actorProxy = Mock.Of<IActorEventPublisher>();
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => actorProxy.SubscribeAsync(subscriber));
            Assert.Equal("actorProxy", exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Delegates to internal, non-virtual ActorProxy.SubscribeAsync requiring Fabric runtime state.
        public Task SubscribesWhenActorProxyIsActorProxyAndTEventImplementsIActorEvents()
        {
            // The success path forwards to ActorProxy.SubscribeAsync(Type, object, TimeSpan), which is internal and
            // non-virtual and depends on servicePartitionClientV2, ActorEventSubscriberManager, and a background
            // resubscription loop. It cannot be observed through Moq or Inspector without out-of-scope SUT testability
            // improvements.
            throw new NotImplementedException();
        }
    }

    public sealed class SubscribeAsync_IActorEventPublisher_TEvent_TimeSpan : ActorProxyEventExtensionsTest
    {
        readonly IActorEventPublisher actorProxy = new TestProxy();
        readonly IActorEvents subscriber = Mock.Of<IActorEvents>();
        readonly TimeSpan resubscriptionInterval = fuzzy.TimeSpan();

        [Fact]
        public async Task ThrowsArgumentExceptionWhenActorProxyIsNotActorProxy()
        {
            IActorEventPublisher actorProxy = Mock.Of<IActorEventPublisher>();
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => actorProxy.SubscribeAsync(subscriber, resubscriptionInterval));
            Assert.Equal("actorProxy", exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Delegates to internal, non-virtual ActorProxy.SubscribeAsync requiring Fabric runtime state.
        public Task SubscribesWhenActorProxyIsActorProxyAndTEventImplementsIActorEvents()
        {
            // The success path forwards to ActorProxy.SubscribeAsync(Type, object, TimeSpan), which is internal and
            // non-virtual and depends on servicePartitionClientV2, ActorEventSubscriberManager, and a background
            // resubscription loop. It cannot be observed through Moq or Inspector without out-of-scope SUT testability
            // improvements.
            throw new NotImplementedException();
        }
    }

    public sealed class UnsubscribeAsync : ActorProxyEventExtensionsTest
    {
        readonly IActorEventPublisher actorProxy = new TestProxy();
        readonly IActorEvents subscriber = Mock.Of<IActorEvents>();

        [Fact]
        public async Task ThrowsArgumentExceptionWhenActorProxyIsNotActorProxy()
        {
            IActorEventPublisher actorProxy = Mock.Of<IActorEventPublisher>();

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => actorProxy.UnsubscribeAsync(subscriber));

            Assert.Equal("actorProxy", exception.ParamName);
        }

        [Fact]
        public async Task ThrowsArgumentExceptionWhenTEventDoesNotImplementIActorEvents()
        {
            string subscriber = fuzzy.String();

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => actorProxy.UnsubscribeAsync(subscriber));

            Assert.Null(exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Delegates to internal, non-virtual ActorProxy.UnsubscribeAsync requiring Fabric runtime state.
        public Task UnsubscribesWhenActorProxyIsActorProxyAndTEventImplementsIActorEvents()
        {
            // The success path forwards to ActorProxy.UnsubscribeAsync(Type, object), which is internal and non-virtual
            // and depends on servicePartitionClientV2 and ActorEventSubscriberManager. It cannot be observed through
            // Moq or Inspector without out-of-scope SUT testability improvements.
            throw new NotImplementedException();
        }
    }

    sealed class TestProxy : ActorProxy, IActorEventPublisher
    {
        protected override object GetReturnValue(int interfaceId, int methodId, object responseBody) => null;
    }
}
