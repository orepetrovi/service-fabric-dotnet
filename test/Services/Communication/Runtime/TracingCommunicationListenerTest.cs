// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Microsoft.ServiceFabric.Diagnostics.Tracing;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.Runtime;

public abstract class TracingCommunicationListenerTest
{
    readonly ICommunicationListener sut;

    // Constructor parameters
    readonly CommunicationListenerInfo original;
    readonly Mock<ITrace> trace = new();

    // Test fixture
    static readonly IFuzz fuzzy = new RandomFuzz();
    readonly Mock<ICommunicationListener> listener = new();
    readonly List<string> events = new();

    TracingCommunicationListenerTest()
    {
        original = new CommunicationListenerInfo(fuzzy.String(), listener.Object);
        sut = new TracingCommunicationListener(original, trace.Object);

        // Record post-construction trace calls in order to prove that the start trace happens before the
        // listener call and the completion/failure trace happens after.
        _ = trace.Setup(_ => _.Info(It.IsAny<string>())).Callback((string m) => events.Add($"info:{m}"));
        _ = trace.Setup(_ => _.Warning(It.IsAny<string>())).Callback((string m) => events.Add($"warning:{m}"));
        _ = trace.Setup(_ => _.Error(It.IsAny<string>())).Callback((string m) => events.Add($"error:{m}"));
    }

    public sealed class Abort : TracingCommunicationListenerTest
    {
        [Fact]
        public void TracesInfoWhenListenerCompletes()
        {
            _ = listener.Setup(_ => _.Abort()).Callback(() => events.Add("listener"));

            sut.Abort();

            Assert.Equal(
                new[] { $"info:Aborting {original}...", "listener", $"info:Aborted {original}." },
                events);
            listener.Verify(_ => _.Abort(), Times.Once);
        }

        [Fact]
        public void TracesErrorWhenListenerThrows()
        {
            var expectedException = new TestException(fuzzy.String());
            _ = listener.Setup(_ => _.Abort()).Callback(() => events.Add("listener")).Throws(expectedException);

            var actualException = Assert.Throws<TestException>(() => sut.Abort());

            Assert.Same(expectedException, actualException);
            Assert.Equal(
                new[] { $"info:Aborting {original}...", "listener", $"error:Abort of {original} failed: {expectedException}" },
                events);
            listener.Verify(_ => _.Abort(), Times.Once);
        }
    }

    public sealed class CloseAsync : TracingCommunicationListenerTest
    {
        readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

        [Fact]
        public async Task TracesInfoWhenListenerCompletes()
        {
            _ = listener.Setup(_ => _.CloseAsync(cancellation)).Callback(() => events.Add("listener")).Returns(Task.CompletedTask);

            await sut.CloseAsync(cancellation);

            Assert.Equal(
                new[] { $"info:Closing {original}...", "listener", $"info:Closed {original}." },
                events);
            listener.Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TracesWarningWhenListenerThrows()
        {
            var expectedException = new TestException(fuzzy.String());
            _ = listener.Setup(_ => _.CloseAsync(cancellation)).Callback(() => events.Add("listener")).Throws(expectedException);

            var actualException = await Assert.ThrowsAsync<TestException>(() => sut.CloseAsync(cancellation));

            Assert.Same(expectedException, actualException);
            Assert.Equal(
                new[] { $"info:Closing {original}...", "listener", $"warning:Closing of {original} failed: {expectedException}" },
                events);
            listener.Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    public sealed class Constructor : TracingCommunicationListenerTest
    {
        [Fact]
        public void ThrowsArgumentNullExceptionWhenOriginalIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TracingCommunicationListener(null, trace.Object));
            Assert.Equal(nameof(original), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenTraceIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TracingCommunicationListener(original, null));
            Assert.Equal(nameof(trace), exception.ParamName);
        }

        [Fact]
        public void TracesCreationInfo()
        {
            trace.Verify(_ => _.Info($"Created {original} of type '{original.Listener.GetType().AssemblyQualifiedName}'."), Times.Once);
            trace.Verify(_ => _.Info(It.IsAny<string>()), Times.Once);
        }
    }

    public sealed class OpenAsync : TracingCommunicationListenerTest
    {
        readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

        [Fact]
        public async Task TracesInfoWhenListenerCompletes()
        {
            string expectedEndpoint = fuzzy.String();
            _ = listener.Setup(_ => _.OpenAsync(cancellation)).Callback(() => events.Add("listener")).Returns(Task.FromResult(expectedEndpoint));

            string actualEndpoint = await sut.OpenAsync(cancellation);

            Assert.Same(expectedEndpoint, actualEndpoint);
            Assert.Equal(
                new[] { $"info:Opening {original}...", "listener", $"info:Opened {original} on endpoint '{expectedEndpoint}'." },
                events);
            listener.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TracesWarningWhenListenerThrows()
        {
            var expectedException = new TestException(fuzzy.String());
            _ = listener.Setup(_ => _.OpenAsync(cancellation)).Callback(() => events.Add("listener")).Throws(expectedException);

            var actualException = await Assert.ThrowsAsync<TestException>(() => sut.OpenAsync(cancellation));

            Assert.Same(expectedException, actualException);
            Assert.Equal(
                new[] { $"info:Opening {original}...", "listener", $"warning:Opening of {original} failed: {expectedException}" },
                events);
            listener.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    sealed class TestException : Exception
    {
        internal TestException(string message) : base(message) { }

        // Stable representation independent of throw history so tests can compare formatted trace
        // messages built before the exception is thrown against those formatted by the product after
        // the exception is thrown (which would otherwise include rethrow stack frames).
        public override string ToString() => $"{nameof(TestException)}: {Message}";
    }
}
