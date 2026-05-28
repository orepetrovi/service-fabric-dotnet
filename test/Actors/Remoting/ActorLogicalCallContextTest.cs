// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Actors.Remoting;

public abstract class ActorLogicalCallContextTest : IDisposable
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ActorLogicalCallContextTest() =>
        ActorLogicalCallContext.Clear();

    void IDisposable.Dispose() =>
        ActorLogicalCallContext.Clear();

    public sealed class IsPresent : ActorLogicalCallContextTest
    {
        [Fact]
        public void ReturnsFalseWhenValueIsNotSet() =>
            Assert.False(ActorLogicalCallContext.IsPresent());

        [Fact]
        public void ReturnsTrueWhenValueIsSet()
        {
            ActorLogicalCallContext.Set(fuzzy.String());
            Assert.True(ActorLogicalCallContext.IsPresent());
        }
    }

    public sealed class TryGet : ActorLogicalCallContextTest
    {
        [Fact]
        public void ReturnsFalseAndNullWhenValueIsNotSet()
        {
            bool result = ActorLogicalCallContext.TryGet(out string value);
            Assert.False(result);
            Assert.Null(value);
        }

        [Fact]
        public void ReturnsTrueAndValueWhenValueIsSet()
        {
            string expected = fuzzy.String();
            ActorLogicalCallContext.Set(expected);

            bool result = ActorLogicalCallContext.TryGet(out string value);

            Assert.True(result);
            Assert.Same(expected, value);
        }
    }

    public sealed class Set : ActorLogicalCallContextTest
    {
        [Fact]
        public void StoresValueObservableByTryGet()
        {
            string expected = fuzzy.String();

            ActorLogicalCallContext.Set(expected);

            ActorLogicalCallContext.TryGet(out string actual);
            Assert.Same(expected, actual);
        }

        [Fact]
        public void OverwritesPreviousValue()
        {
            string previous = fuzzy.String();
            ActorLogicalCallContext.Set(previous);
            string expected = previous + fuzzy.String();

            ActorLogicalCallContext.Set(expected);

            ActorLogicalCallContext.TryGet(out string actual);
            Assert.Same(expected, actual);
        }

        [Fact]
        public async Task StoresValueObservableByAwaitedTask()
        {
            string expected = fuzzy.String();
            ActorLogicalCallContext.Set(expected);

            string actual = await Task.Run(() =>
            {
                ActorLogicalCallContext.TryGet(out string value);
                return value;
            }, TestContext.Current.CancellationToken);

            Assert.Same(expected, actual);
        }

        [Fact]
        public async Task StoresValueObservableAfterAwait()
        {
            string expected = fuzzy.String();
            ActorLogicalCallContext.Set(expected);

            await Task.Yield();

            ActorLogicalCallContext.TryGet(out string actual);
            Assert.Same(expected, actual);
        }

        [Fact]
        public async Task InAwaitedTaskDoesNotAffectCaller()
        {
            string expected = fuzzy.String();
            ActorLogicalCallContext.Set(expected);

            await Task.Run(() => ActorLogicalCallContext.Set(fuzzy.String()), TestContext.Current.CancellationToken);

            ActorLogicalCallContext.TryGet(out string actual);
            Assert.Same(expected, actual);
        }
    }

    public sealed class Clear : ActorLogicalCallContextTest
    {
        [Fact]
        public void RemovesPreviouslySetValue()
        {
            ActorLogicalCallContext.Set(fuzzy.String());

            ActorLogicalCallContext.Clear();

            Assert.False(ActorLogicalCallContext.TryGet(out string value));
            Assert.Null(value);
        }

        [Fact]
        public void IsNoOpWhenValueIsNotSet()
        {
            ActorLogicalCallContext.Clear();
            Assert.False(ActorLogicalCallContext.TryGet(out string value));
            Assert.Null(value);
        }

        [Fact]
        public async Task InAwaitedTaskDoesNotAffectCaller()
        {
            string expected = fuzzy.String();
            ActorLogicalCallContext.Set(expected);

            await Task.Run(() => ActorLogicalCallContext.Clear(), TestContext.Current.CancellationToken);

            ActorLogicalCallContext.TryGet(out string actual);
            Assert.Same(expected, actual);
        }
    }
}
