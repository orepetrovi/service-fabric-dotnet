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

public abstract class FabricTransportConnectionHandlerBrokerTest
{
    readonly NativeFabricTransport.IFabricTransportConnectionHandler sut;

    // Constructor parameters
    readonly Mock<IFabricTransportConnectionHandler> serviceConnectionHandler = new();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);
    static readonly TimeSpan callbackWait = TimeSpan.FromSeconds(5);

    FabricTransportConnectionHandlerBrokerTest() =>
        sut = new FabricTransportConnectionHandlerBroker(serviceConnectionHandler.Object);

    public sealed class Constructor: FabricTransportConnectionHandlerBrokerTest
    {
        [Fact(Explicit = true)] // TODO: SUT bug. Constructor does not validate serviceConnectionHandler.
        public void ThrowsArgumentNullExceptionWhenServiceConnectionHandlerIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new FabricTransportConnectionHandlerBroker(null));
            Assert.Equal(nameof(serviceConnectionHandler), exception.ParamName);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class BeginProcessConnect: FabricTransportConnectionHandlerBrokerTest, IDisposable
    {
        // Method parameters
        readonly NativeFabricTransport.IFabricTransportClientConnection nativeClientConnection;
        readonly uint timeoutMilliseconds = fuzzy.UInt32();
        readonly IFabricAsyncOperationCallback callback = Mock.Of<IFabricAsyncOperationCallback>();

        readonly IntPtr nativeClientId;
        readonly string clientId = fuzzy.String();

        public BeginProcessConnect()
        {
            nativeClientId = Marshal.StringToHGlobalUni(clientId);
            nativeClientConnection = Mock.Of<NativeFabricTransport.IFabricTransportClientConnection>(_ => _.get_ClientId() == nativeClientId);
        }

        void IDisposable.Dispose() => Marshal.FreeHGlobal(nativeClientId);

        [Fact]
        public void InvokesConnectAsyncOnHandlerWithCallbackClientAndManagedTimeout()
        {
            FabricTransportCallbackClient actualClient = null;
            TimeSpan actualTimeout = default;
            _ = serviceConnectionHandler
                .Setup(_ => _.ConnectAsync(It.IsAny<FabricTransportCallbackClient>(), It.IsAny<TimeSpan>()))
                .Callback<FabricTransportCallbackClient, TimeSpan>((c, t) => { actualClient = c; actualTimeout = t; })
                .Returns(Task.FromResult<object>(null));

            _ = sut.BeginProcessConnect(nativeClientConnection, timeoutMilliseconds, callback);

            Assert.Equal(clientId, actualClient.GetClientId());
            Assert.Equal(TimeSpan.FromMilliseconds(timeoutMilliseconds), actualTimeout);
            serviceConnectionHandler.Verify(
                _ => _.ConnectAsync(It.IsAny<FabricTransportCallbackClient>(), It.IsAny<TimeSpan>()),
                Times.Once);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. BeginProcessConnect does not validate nativeClientConnection.
        public void ThrowsArgumentNullExceptionWhenNativeClientConnectionIsNull()
        {
            // BeginProcessConnect passes nativeClientConnection to the FabricTransportCallbackClient constructor, which
            // stores it and later dereferences it via get_ClientId(), throwing NullReferenceException instead of
            // ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.BeginProcessConnect(null, timeoutMilliseconds, callback));
            Assert.Equal(nameof(nativeClientConnection), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. BeginProcessConnect does not validate callback.
        public void ThrowsArgumentNullExceptionWhenCallbackIsNull()
        {
            // BeginProcessConnect passes callback to Utility.WrapNativeAsyncMethodImplementation without validating it,
            // so no ArgumentNullException is thrown.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.BeginProcessConnect(nativeClientConnection, timeoutMilliseconds, null));
            Assert.Equal(nameof(callback), exception.ParamName);
        }

        [Fact]
        public async Task InvokesCallbackWithReturnedContextWhenTaskCompletes()
        {
            var tcs = new TaskCompletionSource<object>();
            _ = serviceConnectionHandler
                .Setup(_ => _.ConnectAsync(It.IsAny<FabricTransportCallbackClient>(), TimeSpan.FromMilliseconds(timeoutMilliseconds)))
                .Returns(tcs.Task);
            var callbackInvoked = new TaskCompletionSource<IFabricAsyncOperationContext>();
            _ = Mock.Get(callback)
                .Setup(_ => _.Invoke(It.IsAny<IFabricAsyncOperationContext>()))
                .Callback<IFabricAsyncOperationContext>(c => callbackInvoked.TrySetResult(c));

            IFabricAsyncOperationContext returnedContext = sut.BeginProcessConnect(nativeClientConnection, timeoutMilliseconds, callback);
            Assert.False(callbackInvoked.Task.IsCompleted);

            tcs.SetResult(null);

            Task completed = await Task.WhenAny(callbackInvoked.Task, Task.Delay(callbackWait, TestContext.Current.CancellationToken));
            Assert.Same(callbackInvoked.Task, completed);
            Assert.Same(returnedContext, await callbackInvoked.Task);
            Mock.Get(callback).Verify(_ => _.Invoke(It.IsAny<IFabricAsyncOperationContext>()), Times.Once);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class BeginProcessDisconnect: FabricTransportConnectionHandlerBrokerTest, IDisposable
    {
        // Method parameters
        readonly IntPtr nativeClientId;
        readonly uint timeoutMilliseconds = fuzzy.UInt32();
        readonly IFabricAsyncOperationCallback callback = Mock.Of<IFabricAsyncOperationCallback>();

        readonly string clientId = fuzzy.String();

        public BeginProcessDisconnect() =>
            nativeClientId = Marshal.StringToHGlobalUni(clientId);

        void IDisposable.Dispose() => Marshal.FreeHGlobal(nativeClientId);

        [Fact]
        public void InvokesDisconnectAsyncOnHandlerWithManagedClientIdAndTimeout()
        {
            string actualClientId = null;
            TimeSpan actualTimeout = default;
            _ = serviceConnectionHandler
                .Setup(_ => _.DisconnectAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Callback<string, TimeSpan>((id, t) => { actualClientId = id; actualTimeout = t; })
                .Returns(Task.FromResult<object>(null));

            _ = sut.BeginProcessDisconnect(nativeClientId, timeoutMilliseconds, callback);

            Assert.Equal(clientId, actualClientId);
            Assert.Equal(TimeSpan.FromMilliseconds(timeoutMilliseconds), actualTimeout);
            serviceConnectionHandler.Verify(
                _ => _.DisconnectAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()),
                Times.Once);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. BeginProcessDisconnect does not validate nativeClientId.
        public void ThrowsArgumentNullExceptionWhenNativeClientIdIsZero()
        {
            // BeginProcessDisconnect passes nativeClientId to NativeTypes.FromNativeString, which returns null for
            // IntPtr.Zero, so a null clientId is silently forwarded to DisconnectAsync instead of throwing
            // ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.BeginProcessDisconnect(IntPtr.Zero, timeoutMilliseconds, callback));
            Assert.Equal(nameof(nativeClientId), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. BeginProcessDisconnect does not validate callback.
        public void ThrowsArgumentNullExceptionWhenCallbackIsNull()
        {
            // BeginProcessDisconnect passes callback to Utility.WrapNativeAsyncMethodImplementation without validating
            // it, so no ArgumentNullException is thrown.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.BeginProcessDisconnect(nativeClientId, timeoutMilliseconds, null));
            Assert.Equal(nameof(callback), exception.ParamName);
        }

        [Fact]
        public async Task InvokesCallbackWithReturnedContextWhenTaskCompletes()
        {
            var tcs = new TaskCompletionSource<object>();
            _ = serviceConnectionHandler
                .Setup(_ => _.DisconnectAsync(clientId, TimeSpan.FromMilliseconds(timeoutMilliseconds)))
                .Returns(tcs.Task);
            var callbackInvoked = new TaskCompletionSource<IFabricAsyncOperationContext>();
            _ = Mock.Get(callback)
                .Setup(_ => _.Invoke(It.IsAny<IFabricAsyncOperationContext>()))
                .Callback<IFabricAsyncOperationContext>(c => callbackInvoked.TrySetResult(c));

            IFabricAsyncOperationContext returnedContext = sut.BeginProcessDisconnect(nativeClientId, timeoutMilliseconds, callback);
            Assert.False(callbackInvoked.Task.IsCompleted);

            tcs.SetResult(null);

            Task completed = await Task.WhenAny(callbackInvoked.Task, Task.Delay(callbackWait, TestContext.Current.CancellationToken));
            Assert.Same(callbackInvoked.Task, completed);
            Assert.Same(returnedContext, await callbackInvoked.Task);
            Mock.Get(callback).Verify(_ => _.Invoke(It.IsAny<IFabricAsyncOperationContext>()), Times.Once);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class EndProcessConnect: FabricTransportConnectionHandlerBrokerTest, IDisposable
    {
        // BeginProcessConnect parameters
        readonly NativeFabricTransport.IFabricTransportClientConnection nativeClientConnection;
        readonly uint timeoutMilliseconds = fuzzy.UInt32();
        readonly IFabricAsyncOperationCallback callback = Mock.Of<IFabricAsyncOperationCallback>();

        readonly IntPtr nativeClientId;

        public EndProcessConnect()
        {
            nativeClientId = Marshal.StringToHGlobalUni(fuzzy.String());
            nativeClientConnection = Mock.Of<NativeFabricTransport.IFabricTransportClientConnection>(_ => _.get_ClientId() == nativeClientId);
        }

        void IDisposable.Dispose() => Marshal.FreeHGlobal(nativeClientId);

        [Fact]
        public void ReturnsWhenWrappedTaskSucceeds()
        {
            _ = serviceConnectionHandler
                .Setup(_ => _.ConnectAsync(It.IsAny<FabricTransportCallbackClient>(), TimeSpan.FromMilliseconds(timeoutMilliseconds)))
                .Returns(Task.FromResult<object>(null));
            IFabricAsyncOperationContext context = sut.BeginProcessConnect(nativeClientConnection, timeoutMilliseconds, callback);

            sut.EndProcessConnect(context);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. EndProcessConnect does not validate context.
        public void ThrowsArgumentNullExceptionWhenContextIsNull()
        {
            // EndProcessConnect passes context straight to AsyncTaskCallInAdapter.End, which validates its own
            // parameter and throws ArgumentNullException with ParamName "adapter". The broker should validate its own
            // context parameter first and throw ArgumentNullException with ParamName "context".
            var exception = Assert.Throws<ArgumentNullException>(() => sut.EndProcessConnect(null));
            Assert.Equal(
                sut.Method<Action<IFabricAsyncOperationContext>>(nameof(NativeFabricTransport.IFabricTransportConnectionHandler.EndProcessConnect))
                    .Parameter<IFabricAsyncOperationContext>().Name,
                exception.ParamName);
        }

        [Fact]
        public void ThrowsExceptionFromFaultedWrappedTask()
        {
            var expected = new InvalidOperationException(fuzzy.String());
            _ = serviceConnectionHandler
                .Setup(_ => _.ConnectAsync(It.IsAny<FabricTransportCallbackClient>(), TimeSpan.FromMilliseconds(timeoutMilliseconds)))
                .Returns(Task.FromException(expected));
            IFabricAsyncOperationContext context = sut.BeginProcessConnect(nativeClientConnection, timeoutMilliseconds, callback);

            var actual = Assert.Throws<InvalidOperationException>(() => sut.EndProcessConnect(context));
            Assert.Same(expected, actual);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class EndProcessDisconnect: FabricTransportConnectionHandlerBrokerTest, IDisposable
    {
        // BeginProcessDisconnect parameters
        readonly IntPtr nativeClientId;
        readonly uint timeoutMilliseconds = fuzzy.UInt32();
        readonly IFabricAsyncOperationCallback callback = Mock.Of<IFabricAsyncOperationCallback>();

        readonly string clientId = fuzzy.String();

        public EndProcessDisconnect() =>
            nativeClientId = Marshal.StringToHGlobalUni(clientId);

        void IDisposable.Dispose() => Marshal.FreeHGlobal(nativeClientId);

        [Fact]
        public void ReturnsWhenWrappedTaskSucceeds()
        {
            _ = serviceConnectionHandler
                .Setup(_ => _.DisconnectAsync(clientId, TimeSpan.FromMilliseconds(timeoutMilliseconds)))
                .Returns(Task.FromResult<object>(null));
            IFabricAsyncOperationContext context = sut.BeginProcessDisconnect(nativeClientId, timeoutMilliseconds, callback);

            sut.EndProcessDisconnect(context);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. EndProcessDisconnect does not validate context.
        public void ThrowsArgumentNullExceptionWhenContextIsNull()
        {
            // EndProcessDisconnect passes context straight to AsyncTaskCallInAdapter.End, which validates its own
            // parameter and throws ArgumentNullException with ParamName "adapter". The broker should validate its own
            // context parameter first and throw ArgumentNullException with ParamName "context".
            var exception = Assert.Throws<ArgumentNullException>(() => sut.EndProcessDisconnect(null));
            Assert.Equal(
                sut.Method<Action<IFabricAsyncOperationContext>>(nameof(NativeFabricTransport.IFabricTransportConnectionHandler.EndProcessDisconnect))
                    .Parameter<IFabricAsyncOperationContext>().Name,
                exception.ParamName);
        }

        [Fact]
        public void ThrowsExceptionFromFaultedWrappedTask()
        {
            var expected = new InvalidOperationException(fuzzy.String());
            _ = serviceConnectionHandler
                .Setup(_ => _.DisconnectAsync(clientId, TimeSpan.FromMilliseconds(timeoutMilliseconds)))
                .Returns(Task.FromException(expected));
            IFabricAsyncOperationContext context = sut.BeginProcessDisconnect(nativeClientId, timeoutMilliseconds, callback);

            var actual = Assert.Throws<InvalidOperationException>(() => sut.EndProcessDisconnect(context));
            Assert.Same(expected, actual);
        }
    }
}
