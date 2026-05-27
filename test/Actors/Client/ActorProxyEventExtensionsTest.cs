// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Fuzzy;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Actors.Client
{
    public abstract class ActorProxyEventExtensionsTest
    {
        static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

        public sealed class SubscribeAsync_IActorEventPublisher_TEvent : ActorProxyEventExtensionsTest
        {
            // Method parameters
            readonly IActorEventPublisher actorProxy = Mock.Of<IActorEventPublisher>();
            readonly IActorEvents subscriber = Mock.Of<IActorEvents>();

            [Fact]
            public async Task ThrowsArgumentExceptionWhenActorProxyIsNotActorProxy()
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(() => actorProxy.SubscribeAsync(subscriber));
                Assert.Equal("actorProxy", exception.ParamName);
            }
        }

        public sealed class SubscribeAsync_IActorEventPublisher_TEvent_TimeSpan : ActorProxyEventExtensionsTest
        {
            // Method parameters
            readonly IActorEventPublisher actorProxy = Mock.Of<IActorEventPublisher>();
            readonly IActorEvents subscriber = Mock.Of<IActorEvents>();
            readonly TimeSpan resubscriptionInterval = fuzzy.TimeSpan();

            [Fact]
            public async Task ThrowsArgumentExceptionWhenActorProxyIsNotActorProxy()
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(() => actorProxy.SubscribeAsync(subscriber, resubscriptionInterval));
                Assert.Equal("actorProxy", exception.ParamName);
            }
        }

        public sealed class UnsubscribeAsync : ActorProxyEventExtensionsTest
        {
            [Fact]
            public async Task ThrowsArgumentExceptionWhenActorProxyIsNotActorProxy()
            {
                IActorEventPublisher actorProxy = Mock.Of<IActorEventPublisher>();
                IActorEvents subscriber = Mock.Of<IActorEvents>();

                var exception = await Assert.ThrowsAsync<ArgumentException>(() => actorProxy.UnsubscribeAsync(subscriber));

                Assert.Equal("actorProxy", exception.ParamName);
            }

            [Fact]
            public async Task ThrowsArgumentExceptionWhenTEventDoesNotImplementIActorEvents()
            {
                IActorEventPublisher actorProxy = new TestProxy();
                string subscriber = fuzzy.String();

                var exception = await Assert.ThrowsAsync<ArgumentException>(() => actorProxy.UnsubscribeAsync(subscriber));

                Assert.Null(exception.ParamName);
            }
        }

        sealed class TestProxy : ActorProxy, IActorEventPublisher
        {
            protected override object GetReturnValue(int interfaceId, int methodId, object responseBody) => null;
        }
    }
}
