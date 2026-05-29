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
    readonly Mock<ITrace> trace = new Mock<ITrace>();

    // Test fixture
    readonly static IFuzz fuzzy = new RandomFuzz();
    readonly Mock<ICommunicationListener> listener = new Mock<ICommunicationListener>();

    TracingCommunicationListenerTest()
    {
        string name = fuzzy.String();
        original = new CommunicationListenerInfo(name, listener.Object);
        sut = new TracingCommunicationListener(original, trace.Object);
    }

    public sealed class Constructor : TracingCommunicationListenerTest
    {
        [Fact]
        public void ThrowsArgumentNullExceptionWhenListenerInfoIsNull()
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
        public void TracesListenerTypeNameAndHashCode()
        {
            trace.Verify(_ => _.Info($"Created {original} of type '{original.Listener.GetType().AssemblyQualifiedName}'."), Times.Once);
        }
    }

    public sealed class Abort : TracingCommunicationListenerTest
    {
        [Fact]
        public void TracesInfoWhenMethodIsCompletedSuccessfully()
        {
            sut.Abort();

            trace.Verify(_ => _.Info($"Aborting {original}..."), Times.Once);
            listener.Verify(_ => _.Abort(), Times.Once);
            trace.Verify(_ => _.Info($"Aborted {original}."), Times.Once);
        }   

        [Fact]
        public void TracesErrorWhenMethodThrowsException()
        {
            var expectedException = new TestException(fuzzy.String());
            listener.Setup(_ => _.Abort()).Throws(expectedException);
            string actualError = null;
            trace.Setup(_ => _.Error(It.IsAny<string>())).Callback((string message) => { actualError = message; });

            var actualException = Assert.Throws<TestException>(() => sut.Abort());

            trace.Verify(_ => _.Info($"Aborting {original}..."), Times.Once);
            Assert.Same(expectedException, actualException);
            string expectedError = $"Abort of {original} failed: ";
            Assert.StartsWith(expectedError, actualError);
        }
    }

    public sealed class CloseAsync : TracingCommunicationListenerTest
    {
        readonly CancellationToken cancellation = new CancellationToken();

        [Fact]
        public async Task TracesInfoWhenMethodIsCompletedSuccessfully()
        {
            await sut.CloseAsync(cancellation);

            trace.Verify(_ => _.Info($"Closing {original}..."), Times.Once);
            listener.Verify(_ => _.CloseAsync(cancellation), Times.Once);
            trace.Verify(_ => _.Info($"Closed {original}."), Times.Once);
        }

        [Fact]
        public async Task TracesWarningWhenMethodThrowsException()
        {
            var expectedException = new TestException(fuzzy.String());
            listener.Setup(_ => _.CloseAsync(cancellation)).Throws(expectedException);
            string actualWarning = null;
            trace.Setup(_ => _.Warning(It.IsAny<string>())).Callback((string message) => { actualWarning = message; });

            var actualException = await Assert.ThrowsAsync<TestException>(() => sut.CloseAsync(cancellation));

            trace.Verify(_ => _.Info($"Closing {original}..."), Times.Once);
            Assert.Same(expectedException, actualException);
            string expectedWarning = $"Closing of {original} failed: ";
            Assert.StartsWith(expectedWarning, actualWarning);
        }
    }

    public sealed class OpenAsync : TracingCommunicationListenerTest
    {
        readonly CancellationToken cancellation = new CancellationToken();

        [Fact]
        public async Task TracesInfoWhenMethodIsCompletedSuccessfully()
        {
            string expectedEndpoint = fuzzy.String();
            listener.Setup(_ => _.OpenAsync(cancellation)).Returns(Task.FromResult(expectedEndpoint));

            string actualEndpoint = await sut.OpenAsync(cancellation);

            trace.Verify(_ => _.Info($"Opening {original}..."), Times.Once);
            Assert.Equal(expectedEndpoint, actualEndpoint);
            trace.Verify(_ => _.Info($"Opened {original} on endpoint '{expectedEndpoint}'."), Times.Once);
        }

        [Fact]
        public async Task TracesWarningWhenMethodThrowsException()
        {
            var expectedException = new TestException(fuzzy.String());
            listener.Setup(_ => _.OpenAsync(cancellation)).Throws(expectedException);
            string actualWarning = null;
            trace.Setup(_ => _.Warning(It.IsAny<string>())).Callback((string message) => { actualWarning = message; });

            var actualException = await Assert.ThrowsAsync<TestException>(() => sut.OpenAsync(cancellation));

            trace.Verify(_ => _.Info($"Opening {original}..."), Times.Once);
            Assert.Same(expectedException, actualException);
            string expectedWarning = $"Opening of {original} failed: ";
            Assert.StartsWith(expectedWarning, actualWarning);
        }
    }

    class TestException : Exception
    {
        internal TestException(string message) : base(message) { }
    }
}
