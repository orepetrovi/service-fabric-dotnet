// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Fuzzy;
using Inspector;
using Moq;
using Xunit;
using static System.Fabric.Interop.NativeCommon;

namespace Microsoft.ServiceFabric.FabricTransport.Runtime;

public abstract class FabricTransportMessageHandlerBrokerTest
{
    readonly NativeFabricTransport.IFabricTransportMessageHandler sut;

    // Constructor parameters
    readonly Mock<IFabricTransportMessageHandler> service = new();
    readonly Mock<IFabricTransportConnectionHandler> nativeConnectionHandler = new();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    FabricTransportMessageHandlerBrokerTest() =>
        sut = new FabricTransportMessageHandlerBroker(service.Object, nativeConnectionHandler.Object);

    public sealed class Constructor: FabricTransportMessageHandlerBrokerTest
    {
        [Fact(Explicit = true)] // TODO: SUT bug. Constructor does not validate service.
        public void ThrowsArgumentNullExceptionWhenServiceIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new FabricTransportMessageHandlerBroker(null, nativeConnectionHandler.Object));
            Assert.Equal(nameof(service), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Constructor does not validate nativeConnectionHandler.
        public void ThrowsArgumentNullExceptionWhenNativeConnectionHandlerIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new FabricTransportMessageHandlerBroker(service.Object, null));
            Assert.Equal(nameof(nativeConnectionHandler), exception.ParamName);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class BeginProcessRequest: FabricTransportMessageHandlerBrokerTest, IDisposable
    {
        // Method parameters
        readonly IntPtr nativeClientId;
        readonly NativeFabricTransport.IFabricTransportMessage message = Mock.Of<NativeFabricTransport.IFabricTransportMessage>();
        readonly uint timeoutMilliseconds = fuzzy.UInt32();
        readonly IFabricAsyncOperationCallback callback = Mock.Of<IFabricAsyncOperationCallback>();

        readonly string clientId = fuzzy.String();
        static readonly TimeSpan callbackWait = TimeSpan.FromSeconds(5);

        public BeginProcessRequest() =>
            nativeClientId = Marshal.StringToHGlobalUni(clientId);

        void IDisposable.Dispose() => Marshal.FreeHGlobal(nativeClientId);

        [Fact]
        public void InvokesRequestResponseAsyncOnServiceWithManagedContextAndMessage()
        {
            FabricTransportRequestContext actualContext = null;
            FabricTransportMessage actualMessage = null;
            _ = service
                .Setup(_ => _.RequestResponseAsync(It.IsAny<FabricTransportRequestContext>(), It.IsAny<FabricTransportMessage>()))
                .Callback<FabricTransportRequestContext, FabricTransportMessage>((c, m) => { actualContext = c; actualMessage = m; })
                .Returns(new TaskCompletionSource<FabricTransportMessage>().Task);
            FabricTransportCallbackClient expectedCallbackClient = new(Mock.Of<NativeFabricTransport.IFabricTransportClientConnection>());
            _ = nativeConnectionHandler.Setup(_ => _.GetCallBack(clientId)).Returns(expectedCallbackClient);

            _ = sut.BeginProcessRequest(nativeClientId, message, timeoutMilliseconds, callback);

            Assert.Equal(clientId, actualContext.ClientId);
            Assert.Same(expectedCallbackClient, actualContext.GetCallbackClient());
            Assert.Same(message, actualMessage.Field<NativeFabricTransport.IFabricTransportMessage>().Value);
            service.Verify(
                _ => _.RequestResponseAsync(It.IsAny<FabricTransportRequestContext>(), It.IsAny<FabricTransportMessage>()),
                Times.Once);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. BeginProcessRequest does not validate nativeClientId.
        public void ThrowsArgumentNullExceptionWhenNativeClientIdIsZero()
        {
            // BeginProcessRequest passes nativeClientId to NativeTypes.FromNativeString, which returns null for
            // IntPtr.Zero, so a null clientId is silently forwarded into FabricTransportRequestContext instead of
            // throwing ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.BeginProcessRequest(IntPtr.Zero, message, timeoutMilliseconds, callback));
            Assert.Equal(nameof(nativeClientId), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. BeginProcessRequest does not validate message.
        public void ThrowsArgumentNullExceptionWhenMessageIsNull()
        {
            // BeginProcessRequest defers dereferencing message into the lambda passed to
            // Utility.WrapNativeAsyncMethodImplementation, where NativeFabricTransportMessage.ToFabricTransportMessage
            // calls message.GetHeaderAndBodyBuffer, so the NullReferenceException surfaces through EndProcessRequest
            // instead of synchronously throwing ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.BeginProcessRequest(nativeClientId, null, timeoutMilliseconds, callback));
            Assert.Equal(nameof(message), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. BeginProcessRequest does not validate callback.
        public void ThrowsArgumentNullExceptionWhenCallbackIsNull()
        {
            // BeginProcessRequest passes callback to Utility.WrapNativeAsyncMethodImplementation without validating it,
            // so no ArgumentNullException is thrown.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.BeginProcessRequest(nativeClientId, message, timeoutMilliseconds, null));
            Assert.Equal(nameof(callback), exception.ParamName);
        }

        [Fact]
        public async Task InvokesCallbackWithReturnedContextWhenTaskCompletes()
        {
            TaskCompletionSource<FabricTransportMessage> tcs = new();
            _ = service
                .Setup(_ => _.RequestResponseAsync(It.IsAny<FabricTransportRequestContext>(), It.IsAny<FabricTransportMessage>()))
                .Returns(tcs.Task);
            TaskCompletionSource<IFabricAsyncOperationContext> callbackInvoked = new();
            _ = Mock.Get(callback)
                .Setup(_ => _.Invoke(It.IsAny<IFabricAsyncOperationContext>()))
                .Callback<IFabricAsyncOperationContext>(c => callbackInvoked.TrySetResult(c));

            IFabricAsyncOperationContext returnedContext = sut.BeginProcessRequest(nativeClientId, message, timeoutMilliseconds, callback);
            Assert.False(callbackInvoked.Task.IsCompleted);

            tcs.SetResult(new FabricTransportMessage(null, null));

            try
            {
                Task completed = await Task.WhenAny(callbackInvoked.Task, Task.Delay(callbackWait, TestContext.Current.CancellationToken));
                Assert.Same(callbackInvoked.Task, completed);
                Assert.Same(returnedContext, await callbackInvoked.Task);
                Mock.Get(callback).Verify(_ => _.Invoke(It.IsAny<IFabricAsyncOperationContext>()), Times.Once);
            }
            finally
            {
                sut.EndProcessRequest(returnedContext).Dispose();
            }
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class EndProcessRequest: FabricTransportMessageHandlerBrokerTest, IDisposable
    {
        // BeginProcessRequest parameters
        readonly IntPtr nativeClientId;
        readonly NativeFabricTransport.IFabricTransportMessage message = Mock.Of<NativeFabricTransport.IFabricTransportMessage>();
        readonly uint timeoutMilliseconds = fuzzy.UInt32();
        readonly IFabricAsyncOperationCallback callback = Mock.Of<IFabricAsyncOperationCallback>();

        public EndProcessRequest() =>
            nativeClientId = Marshal.StringToHGlobalUni(fuzzy.String());

        void IDisposable.Dispose() => Marshal.FreeHGlobal(nativeClientId);

        [Fact]
        public void ReturnsNativeMessageWrappingReplyWhenRequestResponseAsyncCompletes()
        {
            FabricTransportMessage reply = new(null, null);
            _ = service
                .Setup(_ => _.RequestResponseAsync(It.IsAny<FabricTransportRequestContext>(), It.IsAny<FabricTransportMessage>()))
                .Returns(Task.FromResult(reply));
            IFabricAsyncOperationContext context = sut.BeginProcessRequest(nativeClientId, message, timeoutMilliseconds, callback);

            NativeFabricTransport.IFabricTransportMessage result = sut.EndProcessRequest(context);

            try
            {
                Assert.Same(reply, result.Field<FabricTransportMessage>().Value);
            }
            finally
            {
                result.Dispose();
            }
        }

        [Fact(Explicit = true)] // TODO: SUT bug. EndProcessRequest does not validate context.
        public void ThrowsArgumentNullExceptionWhenContextIsNull()
        {
            // EndProcessRequest passes context straight to AsyncTaskCallInAdapter.End, which validates its own
            // parameter and throws ArgumentNullException with ParamName "adapter". The broker should validate its own
            // context parameter first and throw ArgumentNullException with ParamName "context".
            var exception = Assert.Throws<ArgumentNullException>(() => sut.EndProcessRequest(null));
            Assert.Equal(
                sut.Method<Func<IFabricAsyncOperationContext, NativeFabricTransport.IFabricTransportMessage>>()
                    .Parameter<IFabricAsyncOperationContext>().Name,
                exception.ParamName);
        }

        [Fact]
        public void ThrowsExceptionWhenRequestResponseAsyncFaults()
        {
            InvalidOperationException expected = new(fuzzy.String());
            _ = service
                .Setup(_ => _.RequestResponseAsync(It.IsAny<FabricTransportRequestContext>(), It.IsAny<FabricTransportMessage>()))
                .Returns(Task.FromException<FabricTransportMessage>(expected));
            IFabricAsyncOperationContext context = sut.BeginProcessRequest(nativeClientId, message, timeoutMilliseconds, callback);

            var actual = Assert.Throws<InvalidOperationException>(() => sut.EndProcessRequest(context));
            Assert.Same(expected, actual);
        }
    }

    public sealed class HandleOneWay: FabricTransportMessageHandlerBrokerTest, IDisposable
    {
        // Method parameters
        readonly IntPtr nativeClientId;
        readonly NativeFabricTransport.IFabricTransportMessage message = Mock.Of<NativeFabricTransport.IFabricTransportMessage>();

        readonly string clientId = fuzzy.String();

        public HandleOneWay() =>
            nativeClientId = Marshal.StringToHGlobalUni(clientId);

        void IDisposable.Dispose() => Marshal.FreeHGlobal(nativeClientId);

        [Fact]
        public void InvokesHandleOneWayOnServiceWithManagedContextAndMessage()
        {
            FabricTransportRequestContext actualContext = null;
            FabricTransportMessage actualMessage = null;
            _ = service
                .Setup(_ => _.HandleOneWay(It.IsAny<FabricTransportRequestContext>(), It.IsAny<FabricTransportMessage>()))
                .Callback<FabricTransportRequestContext, FabricTransportMessage>((c, m) => { actualContext = c; actualMessage = m; });
            FabricTransportCallbackClient expectedCallbackClient = new(Mock.Of<NativeFabricTransport.IFabricTransportClientConnection>());
            _ = nativeConnectionHandler.Setup(_ => _.GetCallBack(clientId)).Returns(expectedCallbackClient);

            sut.HandleOneWay(nativeClientId, message);

            Assert.Equal(clientId, actualContext.ClientId);
            Assert.Same(expectedCallbackClient, actualContext.GetCallbackClient());
            Assert.Same(message, actualMessage.Field<NativeFabricTransport.IFabricTransportMessage>().Value);
            service.Verify(
                _ => _.HandleOneWay(It.IsAny<FabricTransportRequestContext>(), It.IsAny<FabricTransportMessage>()),
                Times.Once);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. HandleOneWay does not validate nativeClientId.
        public void ThrowsArgumentNullExceptionWhenNativeClientIdIsZero()
        {
            // HandleOneWay passes nativeClientId to NativeTypes.FromNativeString, which returns null for IntPtr.Zero,
            // so a null clientId is silently forwarded into FabricTransportRequestContext instead of throwing
            // ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.HandleOneWay(IntPtr.Zero, message));
            Assert.Equal(nameof(nativeClientId), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. HandleOneWay does not validate message.
        public void ThrowsArgumentNullExceptionWhenMessageIsNull()
        {
            // HandleOneWay passes message to NativeFabricTransportMessage.ToFabricTransportMessage, which immediately
            // dereferences it via message.GetHeaderAndBodyBuffer, throwing NullReferenceException instead of
            // ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.HandleOneWay(nativeClientId, null));
            Assert.Equal(nameof(message), exception.ParamName);
        }
    }
}
