// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.FabricTransport;

public abstract class FabricTransportRequestBodyTest
{
    readonly FabricTransportRequestBody sut;
    readonly IEnumerable<ArraySegment<byte>> sendBuffers = Mock.Of<IEnumerable<ArraySegment<byte>>>();
    readonly Action disposeAction = Mock.Of<Action>();

    FabricTransportRequestBodyTest() =>
        sut = new FabricTransportRequestBody(sendBuffers, disposeAction);

    public sealed class Constructor_IEnumerableOfArraySegmentOfByte_Action : FabricTransportRequestBodyTest
    {
        [Fact(Explicit = true)] // TODO: SUT bug. Constructor does not validate sendBuffers.
        public void ThrowsArgumentNullExceptionWhenSendBuffersIsNull()
        {
            // The constructor silently stores null in the sendBuffers field. GetBodyBuffers() then returns null,
            // and consumers like NativeFabricTransportMessage that call .Any() on the result throw
            // NullReferenceException far from the original caller, violating the rule in csharp.instructions.md
            // requiring ArgumentException over low-level exceptions for invalid arguments.
            var exception = Assert.Throws<ArgumentNullException>(() => new FabricTransportRequestBody(null, disposeAction));
            Assert.Equal(nameof(sendBuffers), exception.ParamName);
        }
    }

    public sealed class Constructor_Stream : FabricTransportRequestBodyTest
    {
        readonly Stream recievedStream = Mock.Of<Stream>(); // Spelling matches SUT parameter

        [Fact(Explicit = true)] // TODO: SUT bug. Constructor does not validate recievedStream.
        public void ThrowsArgumentNullExceptionWhenRecievedStreamIsNull()
        {
            // The constructor silently stores null in the recievedStream field. GetRecievedStream() then returns
            // null, and consumers that read from the returned Stream throw NullReferenceException far from the
            // original caller, violating the rule in csharp.instructions.md requiring ArgumentException over
            // low-level exceptions for invalid arguments.
            var exception = Assert.Throws<ArgumentNullException>(() => new FabricTransportRequestBody(null));
            Assert.Equal(nameof(recievedStream), exception.ParamName);
        }
    }

    public sealed class Dispose : FabricTransportRequestBodyTest
    {
        [Fact]
        public void InvokesDisposeAction()
        {
            sut.Dispose();
            Mock.Get(disposeAction).Verify(_ => _(), Times.Once);
        }

        [Fact]
        public void DoesNotThrowWhenDisposeActionIsNull() =>
            new FabricTransportRequestBody(sendBuffers, null).Dispose();

        [Fact(Explicit = true)] // TODO: SUT bug. Dispose is not idempotent.
        public void InvokesDisposeActionOnlyOnceWhenCalledMultipleTimes()
        {
            // Dispose() unconditionally invokes disposeAction every time it is called, violating the
            // IDisposable contract which requires subsequent calls to be ignored.
            sut.Dispose();
            sut.Dispose();
            Mock.Get(disposeAction).Verify(_ => _(), Times.Once);
        }
    }

    public sealed class GetBodyBuffers : FabricTransportRequestBodyTest
    {
        [Fact]
        public void ReturnsSendBuffers() =>
            Assert.Same(sendBuffers, sut.GetBodyBuffers());
    }

    public sealed class GetRecievedStream : FabricTransportRequestBodyTest
    {
        new readonly FabricTransportRequestBody sut;
        readonly Stream recievedStream = Mock.Of<Stream>(); // Spelling matches SUT parameter

        public GetRecievedStream() =>
            sut = new FabricTransportRequestBody(recievedStream);

        [Fact]
        public void ReturnsRecievedStream() =>
            Assert.Same(recievedStream, sut.GetRecievedStream());
    }
}
