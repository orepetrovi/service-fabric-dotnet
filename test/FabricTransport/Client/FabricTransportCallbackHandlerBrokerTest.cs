// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.FabricTransport.Client;

public abstract class FabricTransportCallbackHandlerBrokerTest
{
    readonly NativeFabricTransport.IFabricTransportCallbackMessageHandler sut;
    readonly Mock<IFabricTransportCallbackMessageHandler> callImpl = new();

    FabricTransportCallbackHandlerBrokerTest() =>
        sut = new FabricTransportCallbackHandlerBroker(callImpl.Object);

    public sealed class Constructor: FabricTransportCallbackHandlerBrokerTest
    {
        [Fact(Explicit = true)] // TODO: SUT bug. Constructor does not validate callImpl.
        public void ThrowsArgumentNullExceptionWhenCallImplIsNull()
        {
            // The constructor stores callImpl without a null check, so a null argument is accepted here and only
            // dereferenced later by HandleOneWay, producing NullReferenceException instead of ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => new FabricTransportCallbackHandlerBroker(null));
            Assert.Equal(nameof(callImpl), exception.ParamName);
        }
    }

    public sealed class HandleOneWay: FabricTransportCallbackHandlerBrokerTest
    {
        readonly Mock<NativeFabricTransport.IFabricTransportMessage> message = new();

        [Fact]
        public void InvokesOneWayMessageOnCallImplWithConvertedMessage()
        {
            FabricTransportMessage actual = null;
            _ = callImpl
                .Setup(_ => _.OneWayMessage(It.IsAny<FabricTransportMessage>()))
                .Callback((FabricTransportMessage m) => actual = m);

            sut.HandleOneWay(message.Object);

            Assert.NotNull(actual);
            message.Verify(_ => _.GetHeaderAndBodyBuffer(out It.Ref<IntPtr>.IsAny, out It.Ref<uint>.IsAny, out It.Ref<IntPtr>.IsAny), Times.Once);
            callImpl.Verify(_ => _.OneWayMessage(It.IsAny<FabricTransportMessage>()), Times.Once);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. HandleOneWay does not validate message.
        public void ThrowsArgumentNullExceptionWhenMessageIsNull()
        {
            // HandleOneWay passes message straight to NativeFabricTransportMessage.ToFabricTransportMessage, which
            // dereferences it to call GetHeaderAndBodyBuffer and throws NullReferenceException instead of
            // ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.HandleOneWay(null));
            Assert.Equal(nameof(message), exception.ParamName);
        }
    }
}
