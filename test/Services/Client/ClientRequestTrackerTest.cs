using System;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Client;

public abstract class ClientRequestTrackerTest : IDisposable
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ClientRequestTrackerTest() { }

    void IDisposable.Dispose() { }

    public sealed class IsPresent : ClientRequestTrackerTest
    {
        [Fact]
        public void ReturnsTrueWhenValueIsSet()
        {
            ClientRequestTracker.Set(fuzzy.String());
            Assert.True(ClientRequestTracker.IsPresent());
        }

        [Fact]
        public void ReturnsFalseWhenNoValueIsSet() =>
            Assert.False(ClientRequestTracker.IsPresent());

        [Fact]
        public void ReturnsFalseAfterValueIsClearedWithNull()
        {
            ClientRequestTracker.Set(fuzzy.String());
            ClientRequestTracker.Set(null);
            Assert.False(ClientRequestTracker.IsPresent());
        }
    }

    public sealed class Set : ClientRequestTrackerTest
    {
        readonly string callContextValue = fuzzy.String();

        [Fact]
        public void StoresGivenValueRetrievableViaTryGet()
        {
            ClientRequestTracker.Set(callContextValue);

            Assert.True(ClientRequestTracker.TryGet(out string actual));
            Assert.Same(callContextValue, actual);
        }

        [Fact]
        public void ReplacesPreviouslySetValue()
        {
            string replacement = callContextValue + fuzzy.String();
            ClientRequestTracker.Set(callContextValue);

            ClientRequestTracker.Set(replacement);

            Assert.True(ClientRequestTracker.TryGet(out string actual));
            Assert.Same(replacement, actual);
        }
    }

    public sealed class TryGet : ClientRequestTrackerTest
    {
        [Fact]
        public void ReturnsTrueAndOutputsValueWhenValueIsSet()
        {
            string expected = fuzzy.String();
            ClientRequestTracker.Set(expected);

            bool result = ClientRequestTracker.TryGet(out string actual);

            Assert.True(result);
            Assert.Same(expected, actual);
        }

        [Fact]
        public void ReturnsFalseAndOutputsNullWhenNoValueIsSet()
        {
            bool result = ClientRequestTracker.TryGet(out string actual);

            Assert.False(result);
            Assert.Null(actual);
        }
    }
}
