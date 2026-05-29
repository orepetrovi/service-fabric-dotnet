// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
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

    TracingCommunicationListenerTest()
    {
        original = new CommunicationListenerInfo(fuzzy.String(), listener.Object);
        sut = new TracingCommunicationListener(original, trace.Object);
    }

    public sealed class Abort : TracingCommunicationListenerTest
    {
        [Fact]
        public void TracesInfoWhenListenerCompletes()
        {
            sut.Abort();

            trace.Verify(_ => _.Info($"Aborting {original}..."), Times.Once);
            listener.Verify(_ => _.Abort(), Times.Once);
            trace.Verify(_ => _.Info($"Aborted {original}."), Times.Once);
            trace.Verify(_ => _.Info(It.IsAny<string>()), Times.Exactly(3));
        }

        [Fact]
        public void TracesErrorWhenListenerThrows()
        {
            var expectedException = new TestException(fuzzy.String());
            _ = listener.Setup(_ => _.Abort()).Throws(expectedException);
            string actualError = null;
            string expectedError = null;
            _ = trace.Setup(_ => _.Error(It.IsAny<string>())).Callback((string message) =>
            {
                actualError = message;
                expectedError = $"Abort of {original} failed: {expectedException}";
            });

            var actualException = Assert.Throws<TestException>(() => sut.Abort());

            trace.Verify(_ => _.Info($"Aborting {original}..."), Times.Once);
            trace.Verify(_ => _.Info(It.IsAny<string>()), Times.Exactly(2));
            Assert.Same(expectedException, actualException);
            trace.Verify(_ => _.Error(It.IsAny<string>()), Times.Once);
            Assert.Equal(expectedError, actualError);
        }
    }

    public sealed class CloseAsync : TracingCommunicationListenerTest
    {
        readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

        [Fact]
        public async Task TracesInfoWhenListenerCompletes()
        {
            await sut.CloseAsync(cancellation);

            trace.Verify(_ => _.Info($"Closing {original}..."), Times.Once);
            listener.Verify(_ => _.CloseAsync(cancellation), Times.Once);
            listener.Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            trace.Verify(_ => _.Info($"Closed {original}."), Times.Once);
            trace.Verify(_ => _.Info(It.IsAny<string>()), Times.Exactly(3));
        }

        [Fact]
        public async Task TracesWarningWhenListenerThrows()
        {
            var expectedException = new TestException(fuzzy.String());
            _ = listener.Setup(_ => _.CloseAsync(cancellation)).Throws(expectedException);
            string actualWarning = null;
            string expectedWarning = null;
            _ = trace.Setup(_ => _.Warning(It.IsAny<string>())).Callback((string message) =>
            {
                actualWarning = message;
                expectedWarning = $"Closing of {original} failed: {expectedException}";
            });

            var actualException = await Assert.ThrowsAsync<TestException>(() => sut.CloseAsync(cancellation));

            trace.Verify(_ => _.Info($"Closing {original}..."), Times.Once);
            trace.Verify(_ => _.Info(It.IsAny<string>()), Times.Exactly(2));
            Assert.Same(expectedException, actualException);
            listener.Verify(_ => _.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            trace.Verify(_ => _.Warning(It.IsAny<string>()), Times.Once);
            Assert.Equal(expectedWarning, actualWarning);
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
            _ = listener.Setup(_ => _.OpenAsync(cancellation)).Returns(Task.FromResult(expectedEndpoint));

            string actualEndpoint = await sut.OpenAsync(cancellation);

            trace.Verify(_ => _.Info($"Opening {original}..."), Times.Once);
            Assert.Same(expectedEndpoint, actualEndpoint);
            listener.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
            trace.Verify(_ => _.Info($"Opened {original} on endpoint '{expectedEndpoint}'."), Times.Once);
            trace.Verify(_ => _.Info(It.IsAny<string>()), Times.Exactly(3));
        }

        [Fact]
        public async Task TracesWarningWhenListenerThrows()
        {
            var expectedException = new TestException(fuzzy.String());
            _ = listener.Setup(_ => _.OpenAsync(cancellation)).Throws(expectedException);
            string actualWarning = null;
            string expectedWarning = null;
            _ = trace.Setup(_ => _.Warning(It.IsAny<string>())).Callback((string message) =>
            {
                actualWarning = message;
                expectedWarning = $"Opening of {original} failed: {expectedException}";
            });

            var actualException = await Assert.ThrowsAsync<TestException>(() => sut.OpenAsync(cancellation));

            trace.Verify(_ => _.Info($"Opening {original}..."), Times.Once);
            trace.Verify(_ => _.Info(It.IsAny<string>()), Times.Exactly(2));
            Assert.Same(expectedException, actualException);
            listener.Verify(_ => _.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
            trace.Verify(_ => _.Warning(It.IsAny<string>()), Times.Once);
            Assert.Equal(expectedWarning, actualWarning);
        }
    }

    sealed class TestException : Exception
    {
        internal TestException(string message) : base(message) { }
    }
}
