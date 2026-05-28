// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using Fuzzy;
using Microsoft.ServiceFabric.Actors.Tests;
using Microsoft.ServiceFabric.Services.Common;
using Xunit;

namespace Microsoft.ServiceFabric.Actors.Client;

public abstract class SubscriberTest
{
    readonly Subscriber sut;

    // Constructor parameters
    readonly ActorId actorId = fuzzy.ActorId();
    readonly int eventId = fuzzy.Int32();
    readonly object instance = new();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    SubscriberTest() =>
        sut = new Subscriber(actorId, eventId, instance);

    public sealed class Constructor : SubscriberTest
    {
        [Fact]
        public void InitializesProperties()
        {
            Assert.Same(actorId, sut.ActorId);
            Assert.Equal(eventId, sut.EventId);
            Assert.Same(instance, sut.Instance);
        }
    }

    public new sealed class Equals : SubscriberTest
    {
        // Method parameters
        readonly object obj;

        public Equals() =>
            obj = new Subscriber(actorId, eventId, instance);

        [Fact]
        public void ReturnsTrueWhenActorIdEventIdAndInstanceMatch() =>
            Assert.True(sut.Equals(obj));

        [Fact]
        public void ReturnsFalseWhenObjIsNull() =>
            Assert.False(sut.Equals(null));

        [Fact]
        public void ReturnsFalseWhenObjIsNotSubscriber() =>
            Assert.False(sut.Equals(new object()));

        [Fact]
        public void ReturnsFalseWhenActorIdIsDifferent()
        {
            ActorId differentActorId;
            do
                differentActorId = fuzzy.ActorId();
            while (differentActorId.Equals(actorId));

            Assert.False(sut.Equals(new Subscriber(differentActorId, eventId, instance)));
        }

        [Fact]
        public void ReturnsFalseWhenEventIdIsDifferent() =>
            Assert.False(sut.Equals(new Subscriber(actorId, eventId + fuzzy.Int32().Between(1, 5), instance)));

        [Fact]
        public void ReturnsFalseWhenInstanceIsDifferentReference() =>
            Assert.False(sut.Equals(new Subscriber(actorId, eventId, new object())));
    }

    public new sealed class GetHashCode : SubscriberTest
    {
        [Fact]
        public void CombinesHashCodesOfActorIdEventIdAndInstance()
        {
            int expected = IdUtil.HashCombine(
                IdUtil.HashCombine(actorId.GetHashCode(), eventId.GetHashCode()),
                instance.GetHashCode());

            Assert.Equal(expected, sut.GetHashCode());
        }
    }
}
