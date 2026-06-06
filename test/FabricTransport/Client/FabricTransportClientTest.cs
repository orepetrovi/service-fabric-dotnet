// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Inspector;
using Moq;
using Xunit;
using static Microsoft.ServiceFabric.FabricTransport.NativeFabricTransport;
using static System.Fabric.Interop.NativeCommon;

namespace Microsoft.ServiceFabric.FabricTransport.Client;

public abstract class FabricTransportClientTest
{
    readonly FabricTransportClient sut = new TestClient();

    // Constructor parameters
    readonly FabricTransportSettings transportSettings = new();
    readonly string connectionAddress = fuzzy.String();
    readonly IFabricTransportClientEventHandler eventHandler = Mock.Of<IFabricTransportClientEventHandler>();
    readonly IFabricTransportCallbackMessageHandler contract = Mock.Of<IFabricTransportCallbackMessageHandler>();
    readonly IFabricTransportMessageDisposer messageMessageDisposer = Mock.Of<IFabricTransportMessageDisposer>(); // Matches SUT parameter name; SUT typo preserved intentionally.

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    public sealed class Constructor: FabricTransportClientTest
    {
        [Fact(Explicit = true)] // TODO: SUT testability limitation. Constructor P/Invokes into native runtime via CreateNativeClient.
        public void StoresParameterValues() =>
            // The public constructor assigns its connectionAddress parameter to the
            // ConnectionAddress property and its transportSettings parameter to the settings
            // field, but it also calls CreateNativeClient through Utility.WrapNativeSyncInvokeInMTA,
            // which P/Invokes into the native Service Fabric runtime unavailable in the test
            // process. No injection seam bypasses the native call, so the assignments cannot
            // be observed in isolation.
            throw new NotImplementedException();

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Constructor P/Invokes into native runtime via CreateNativeClient.
        public void CreatesNativeClient() =>
            // The public constructor calls CreateNativeClient through Utility.WrapNativeSyncInvokeInMTA,
            // which P/Invokes into the native Service Fabric runtime. The runtime is unavailable
            // in the test process and no injection seam exposes the call for substitution.
            throw new NotImplementedException();

        [Fact(Explicit = true)] // TODO: SUT bug. Constructor does not validate transportSettings.
        public void ThrowsArgumentNullExceptionWhenTransportSettingsIsNull()
        {
            // The public constructor accepts FabricTransportSettings without a null check, then
            // dereferences it inside CreateNativeClient via transportSettings.ToNativeV2(pin) and
            // later through settings.ConnectTimeout. Today the constructor throws NullReferenceException
            // from inside Utility.WrapNativeSyncInvokeInMTA instead of ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => new FabricTransportClient(
                transportSettings: null, connectionAddress, eventHandler, contract, messageMessageDisposer));
            Assert.Equal(nameof(transportSettings), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Constructor does not validate connectionAddress.
        public void ThrowsArgumentNullExceptionWhenConnectionAddressIsNull()
        {
            // The public constructor accepts connectionAddress without a null check, then stores it
            // in the ConnectionAddress property which IsSecurityMismatch dereferences via
            // ConnectionAddress.Contains(...). Today the constructor stores null without validation
            // instead of throwing ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => new FabricTransportClient(
                transportSettings, connectionAddress: null, eventHandler, contract, messageMessageDisposer));
            Assert.Equal(nameof(connectionAddress), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Constructor does not validate eventHandler.
        public void ThrowsArgumentNullExceptionWhenEventHandlerIsNull()
        {
            // The public constructor accepts IFabricTransportClientEventHandler without a null check,
            // then passes it to FabricTransportClientConnectionEventHandlerBroker which stores it
            // and later dereferences it via callbacks. Today the constructor stores null without
            // validation instead of throwing ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => new FabricTransportClient(
                transportSettings, connectionAddress, eventHandler: null, contract, messageMessageDisposer));
            Assert.Equal(nameof(eventHandler), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Constructor does not validate contract.
        public void ThrowsArgumentNullExceptionWhenContractIsNull()
        {
            // The public constructor accepts IFabricTransportCallbackMessageHandler without a null
            // check, then passes it to FabricTransportCallbackMessageHandlerBroker which stores it
            // and later dereferences it when handling callbacks. Today the constructor stores null
            // without validation instead of throwing ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => new FabricTransportClient(
                transportSettings, connectionAddress, eventHandler, contract: null, messageMessageDisposer));
            Assert.Equal(nameof(contract), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Constructor does not validate messageMessageDisposer.
        public void ThrowsArgumentNullExceptionWhenMessageMessageDisposerIsNull()
        {
            // The public constructor accepts IFabricTransportMessageDisposer without a null check,
            // then passes it through to CreateNativeClient. Today the constructor stores null
            // without validation instead of throwing ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => new FabricTransportClient(
                transportSettings, connectionAddress, eventHandler, contract, messageMessageDisposer: null));
            Assert.Equal(nameof(messageMessageDisposer), exception.ParamName);
        }
    }

    public sealed class Abort: FabricTransportClientTest
    {
        [Fact]
        public void InvokesAbortOnNativeClient()
        {
            var nativeClient = Mock.Of<IFabricTransportClient2>();
            sut.Field<IFabricTransportClient2>().Set(nativeClient);

            sut.Abort();

            Mock.Get(nativeClient).Verify(_ => _.Abort(), Times.Once);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class CloseAsync: FabricTransportClientTest
    {
        // Method parameters
        readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

        readonly IFabricTransportClient2 nativeClient = Mock.Of<IFabricTransportClient2>();

        public CloseAsync() => sut.Field<IFabricTransportClient2>().Set(nativeClient);

        [Fact]
        public async Task InvokesBeginCloseAndEndCloseOnNativeClient()
        {
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs) });
            IFabricAsyncOperationCallback capturedCallback = null;
            var context = Mock.Of<IFabricAsyncOperationContext>();
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginClose(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Callback<uint, IFabricAsyncOperationCallback>((_, cb) => capturedCallback = cb)
                .Returns(context);

            Task task = sut.CloseAsync(cancellation);
            capturedCallback.Invoke(context);
            await task;

            Mock.Get(nativeClient).Verify(
                _ => _.BeginClose(It.IsAny<uint>(), It.IsAny<IFabricAsyncOperationCallback>()),
                Times.Once);
            Mock.Get(nativeClient).Verify(_ => _.EndClose(context), Times.Once);
            Mock.Get(nativeClient).Verify(_ => _.EndClose(It.IsAny<IFabricAsyncOperationContext>()), Times.Once);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. CloseAsync ignores cancellationToken.
        public void UsesCancellationToken()
        {
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs) });
            var context = Mock.Of<IFabricAsyncOperationContext>();
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginClose(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Returns(context);
            using var cts = new CancellationTokenSource();

            _ = sut.CloseAsync(cts.Token);
            cts.Cancel();

            Mock.Get(context).Verify(_ => _.Cancel(), Times.Once);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Cannot intercept Utility.WrapNativeAsyncInvokeInMTA to observe operationName.
        public void PassesCloseAsyncAsOperationName() =>
            // The SUT bug: CloseAsync hardcodes "OpenAsync" as the operationName argument to
            // Utility.WrapNativeAsyncInvokeInMTA, so diagnostic output for a close failure would
            // misidentify the operation. The testability limitation: the static
            // Utility.WrapNativeAsyncInvokeInMTA helper is not mockable from the test process and
            // no injection seam exposes the argument, so the operationName cannot be observed.
            throw new NotImplementedException();

        [Fact]
        public async Task WrapsFabricCannotConnectExceptionAsConnectionDeniedWhenSecurityMismatch()
        {
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs) });
            sut.Property<string>().Set("Secure");
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginClose(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(new FabricCannotConnectException(fuzzy.String()));

            _ = await Assert.ThrowsAsync<FabricConnectionDeniedException>(() => sut.CloseAsync(cancellation));
        }

        [Fact]
        public async Task RethrowsFabricCannotConnectExceptionWhenNotSecurityMismatch()
        {
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs) });
            sut.Property<string>().Set(fuzzy.Int64().ToString()); // Digits-only so it never contains the "Secure" marker checked by the SUT.
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginClose(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(new FabricCannotConnectException(fuzzy.String()));

            _ = await Assert.ThrowsAsync<FabricCannotConnectException>(() => sut.CloseAsync(cancellation));
        }

        [Fact]
        public async Task RethrowsFabricCannotConnectExceptionWhenSecureAddressAndNonNoneCredentials()
        {
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs), SecurityCredentials = new X509Credentials() });
            sut.Property<string>().Set("Secure");
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginClose(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(new FabricCannotConnectException(fuzzy.String()));

            _ = await Assert.ThrowsAsync<FabricCannotConnectException>(() => sut.CloseAsync(cancellation));
        }

        [Fact(Explicit = true)] // TODO: SUT bug. IsSecurityMismatch dereferences SecurityCredentials without a null check.
        public async Task WrapsFabricCannotConnectExceptionAsConnectionDeniedWhenSecureAddressAndNullCredentials()
        {
            // Null SecurityCredentials is treated as no credentials everywhere else in the SUT
            // (FabricTransportSettings defaults SecurityCredentials to NoneSecurityCredentials and
            // FabricTransportSettingsExtension converts null as no native credentials), so a secure
            // address combined with null credentials is a security mismatch and should be wrapped
            // as FabricConnectionDeniedException. IsSecurityMismatch, however, dereferences
            // settings.SecurityCredentials.CredentialType without a null check and throws
            // NullReferenceException, so this test cannot pass against the current SUT.
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs), SecurityCredentials = null });
            sut.Property<string>().Set("Secure");
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginClose(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(new FabricCannotConnectException(fuzzy.String()));

            _ = await Assert.ThrowsAsync<FabricConnectionDeniedException>(() => sut.CloseAsync(cancellation));
        }

        [Fact]
        public async Task RethrowsOtherExceptions()
        {
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs) });
            var expected = new InvalidOperationException(fuzzy.String());
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginClose(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CloseAsync(cancellation));
            Assert.Same(expected, actual);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Utility wraps EndClose exceptions; catch (TimeoutException) is unreachable.
        public void WrapsTimeoutExceptionWithErrorServiceTooBusy() =>
            // When the operation times out, CloseAsync wraps TimeoutException with the
            // ErrorServiceTooBusy message. Empirically, throwing TimeoutException from a mocked
            // EndClose after invoking the captured callback surfaces as System.Fabric.FabricException
            // wrapping a System.Fabric.Interop.Utility+COMWrapperException wrapping the original
            // TimeoutException, because Utility.WrapNativeAsyncInvokeInMTA translates exceptions
            // through COM HResult mapping. The catch (TimeoutException) branch in the SUT is
            // therefore unreachable without substituting the interop helper, which exposes no
            // injection seam.
            throw new NotImplementedException();
    }

    public sealed class Dispose: FabricTransportClientTest
    {
        [Fact]
        public void DoesNothingWhenNativeClientIsNull() =>
            sut.Dispose();

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Utility.FinalReleaseComObject casts to ComObject and throws on mocks.
        public void ReleasesNativeClientAndSetsItToNull() =>
            // Dispose() invokes nativeClient.FinalReleaseComObject(), which forwards to
            // System.Fabric.Interop.Utility.FinalReleaseComObject. That helper unconditionally
            // casts its argument to System.Runtime.InteropServices.Marshalling.ComObject, so a
            // Mock<IFabricTransportClient2> produces an InvalidCastException before the field
            // can be cleared. The SUT exposes no seam to substitute the release call.
            throw new NotImplementedException();
    }

    public sealed class IsValid: FabricTransportClientTest
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsSetToGivenValue(bool value)
        {
            sut.IsValid = value;
            Assert.Equal(value, sut.IsValid);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class OpenAsync: FabricTransportClientTest
    {
        // Method parameters
        readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

        readonly IFabricTransportClient2 nativeClient = Mock.Of<IFabricTransportClient2>();

        public OpenAsync() => sut.Field<IFabricTransportClient2>().Set(nativeClient);

        [Fact]
        public async Task InvokesBeginOpenAndEndOpenOnNativeClient()
        {
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs) });
            IFabricAsyncOperationCallback capturedCallback = null;
            var context = Mock.Of<IFabricAsyncOperationContext>();
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginOpen(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Callback<uint, IFabricAsyncOperationCallback>((_, cb) => capturedCallback = cb)
                .Returns(context);

            Task task = sut.OpenAsync(cancellation);
            capturedCallback.Invoke(context);
            await task;

            Mock.Get(nativeClient).Verify(
                _ => _.BeginOpen(It.IsAny<uint>(), It.IsAny<IFabricAsyncOperationCallback>()),
                Times.Once);
            Mock.Get(nativeClient).Verify(_ => _.EndOpen(context), Times.Once);
            Mock.Get(nativeClient).Verify(_ => _.EndOpen(It.IsAny<IFabricAsyncOperationContext>()), Times.Once);
        }

        [Fact]
        public void UsesCancellationToken()
        {
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs) });
            var context = Mock.Of<IFabricAsyncOperationContext>();
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginOpen(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Returns(context);
            using var cts = new CancellationTokenSource();

            _ = sut.OpenAsync(cts.Token);
            cts.Cancel();

            Mock.Get(context).Verify(_ => _.Cancel(), Times.Once);
        }

        [Fact]
        public async Task WrapsFabricCannotConnectExceptionAsConnectionDeniedWhenSecurityMismatch()
        {
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs) });
            sut.Property<string>().Set("Secure");
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginOpen(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(new FabricCannotConnectException(fuzzy.String()));

            _ = await Assert.ThrowsAsync<FabricConnectionDeniedException>(() => sut.OpenAsync(cancellation));
        }

        [Fact]
        public async Task RethrowsFabricCannotConnectExceptionWhenNotSecurityMismatch()
        {
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs) });
            sut.Property<string>().Set(fuzzy.Int64().ToString()); // Digits-only so it never contains the "Secure" marker checked by the SUT.
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginOpen(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(new FabricCannotConnectException(fuzzy.String()));

            _ = await Assert.ThrowsAsync<FabricCannotConnectException>(() => sut.OpenAsync(cancellation));
        }

        [Fact]
        public async Task RethrowsFabricCannotConnectExceptionWhenSecureAddressAndNonNoneCredentials()
        {
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs), SecurityCredentials = new X509Credentials() });
            sut.Property<string>().Set("Secure");
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginOpen(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(new FabricCannotConnectException(fuzzy.String()));

            _ = await Assert.ThrowsAsync<FabricCannotConnectException>(() => sut.OpenAsync(cancellation));
        }

        [Fact(Explicit = true)] // TODO: SUT bug. IsSecurityMismatch dereferences SecurityCredentials without a null check.
        public async Task WrapsFabricCannotConnectExceptionAsConnectionDeniedWhenSecureAddressAndNullCredentials()
        {
            // Null SecurityCredentials is treated as no credentials everywhere else in the SUT
            // (FabricTransportSettings defaults SecurityCredentials to NoneSecurityCredentials and
            // FabricTransportSettingsExtension converts null as no native credentials), so a secure
            // address combined with null credentials is a security mismatch and should be wrapped
            // as FabricConnectionDeniedException. IsSecurityMismatch, however, dereferences
            // settings.SecurityCredentials.CredentialType without a null check and throws
            // NullReferenceException, so this test cannot pass against the current SUT.
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs), SecurityCredentials = null });
            sut.Property<string>().Set("Secure");
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginOpen(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(new FabricCannotConnectException(fuzzy.String()));

            _ = await Assert.ThrowsAsync<FabricConnectionDeniedException>(() => sut.OpenAsync(cancellation));
        }

        [Fact]
        public async Task RethrowsOtherExceptions()
        {
            uint connectTimeoutMs = fuzzy.UInt32();
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { ConnectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs) });
            var expected = new InvalidOperationException(fuzzy.String());
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginOpen(connectTimeoutMs, It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.OpenAsync(cancellation));
            Assert.Same(expected, actual);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Utility wraps EndOpen exceptions; catch (TimeoutException) is unreachable.
        public void WrapsTimeoutExceptionWithErrorServiceTooBusy() =>
            // When the operation times out, OpenAsync wraps TimeoutException with the
            // ErrorServiceTooBusy message. Empirically, throwing TimeoutException from a mocked
            // EndOpen after invoking the captured callback surfaces as System.Fabric.FabricException
            // wrapping a System.Fabric.Interop.Utility+COMWrapperException wrapping the original
            // TimeoutException, because Utility.WrapNativeAsyncInvokeInMTA translates exceptions
            // through COM HResult mapping. The catch (TimeoutException) branch in the SUT is
            // therefore unreachable without substituting the interop helper, which exposes no
            // injection seam.
            throw new NotImplementedException();
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class RequestResponseAsync: FabricTransportClientTest
    {
        // Method parameters
        readonly FabricTransportMessage requestMessage = CreateMessage();
        readonly TimeSpan timeout = fuzzy.TimeSpan().Milliseconds();
        readonly Guid requestId = Guid.NewGuid();

        readonly IFabricTransportClient2 nativeClient = Mock.Of<IFabricTransportClient2>();

        public RequestResponseAsync()
        {
            sut.Field<IFabricTransportClient2>().Set(nativeClient);
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings());
        }

        [Fact]
        public async Task InvokesBeginRequestAndEndRequestWhenRequestIdIsDefault()
        {
            IFabricAsyncOperationCallback capturedCallback = null;
            IFabricTransportMessage capturedNativeMessage = null;
            var context = Mock.Of<IFabricAsyncOperationContext>();
            var nativeResponse = Mock.Of<IFabricTransportMessage>();
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginRequest(
                    It.IsAny<IFabricTransportMessage>(),
                    (uint)timeout.TotalMilliseconds,
                    It.IsAny<IFabricAsyncOperationCallback>()))
                .Callback<IFabricTransportMessage, uint, IFabricAsyncOperationCallback>(
                    (m, _, cb) => { capturedNativeMessage = m; capturedCallback = cb; })
                .Returns(context);
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.EndRequest(context))
                .Returns(nativeResponse);

            Task<FabricTransportMessage> task = sut.RequestResponseAsync(requestMessage, timeout);
            capturedCallback.Invoke(context);
            FabricTransportMessage result = await task;

            Mock.Get(nativeClient).Verify(
                _ => _.BeginRequest(
                    It.IsAny<IFabricTransportMessage>(),
                    It.IsAny<uint>(),
                    It.IsAny<IFabricAsyncOperationCallback>()),
                Times.Once);
            Mock.Get(nativeClient).Verify(
                _ => _.BeginRequestWithId(
                    It.IsAny<Guid>(),
                    It.IsAny<IFabricTransportMessage>(),
                    It.IsAny<uint>(),
                    It.IsAny<IFabricAsyncOperationCallback>()),
                Times.Never);
            Mock.Get(nativeClient).Verify(_ => _.EndRequest(It.IsAny<IFabricAsyncOperationContext>()), Times.Once);
            Mock.Get(nativeClient).Verify(_ => _.EndRequestWithId(It.IsAny<IFabricAsyncOperationContext>()), Times.Never);
            var wrapper = (NativeFabricTransportMessage)capturedNativeMessage;
            Assert.Same(requestMessage, wrapper.Field<FabricTransportMessage>().Value);
            Assert.Same(nativeResponse, result.Field<IFabricTransportMessage>().Value);
        }

        [Fact]
        public async Task InvokesBeginRequestWithIdAndEndRequestWithIdWhenRequestIdIsNotDefault()
        {
            IFabricAsyncOperationCallback capturedCallback = null;
            IFabricTransportMessage capturedNativeMessage = null;
            var context = Mock.Of<IFabricAsyncOperationContext>();
            var nativeResponse = Mock.Of<IFabricTransportMessage>();
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginRequestWithId(
                    requestId,
                    It.IsAny<IFabricTransportMessage>(),
                    (uint)timeout.TotalMilliseconds,
                    It.IsAny<IFabricAsyncOperationCallback>()))
                .Callback<Guid, IFabricTransportMessage, uint, IFabricAsyncOperationCallback>(
                    (_, m, _, cb) => { capturedNativeMessage = m; capturedCallback = cb; })
                .Returns(context);
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.EndRequestWithId(context))
                .Returns(nativeResponse);

            Task<FabricTransportMessage> task = sut.RequestResponseAsync(requestMessage, timeout, requestId);
            capturedCallback.Invoke(context);
            FabricTransportMessage result = await task;

            Mock.Get(nativeClient).Verify(
                _ => _.BeginRequestWithId(
                    It.IsAny<Guid>(),
                    It.IsAny<IFabricTransportMessage>(),
                    It.IsAny<uint>(),
                    It.IsAny<IFabricAsyncOperationCallback>()),
                Times.Once);
            Mock.Get(nativeClient).Verify(
                _ => _.BeginRequest(
                    It.IsAny<IFabricTransportMessage>(),
                    It.IsAny<uint>(),
                    It.IsAny<IFabricAsyncOperationCallback>()),
                Times.Never);
            Mock.Get(nativeClient).Verify(_ => _.EndRequestWithId(It.IsAny<IFabricAsyncOperationContext>()), Times.Once);
            Mock.Get(nativeClient).Verify(_ => _.EndRequest(It.IsAny<IFabricAsyncOperationContext>()), Times.Never);
            var wrapper = (NativeFabricTransportMessage)capturedNativeMessage;
            Assert.Same(requestMessage, wrapper.Field<FabricTransportMessage>().Value);
            Assert.Same(nativeResponse, result.Field<IFabricTransportMessage>().Value);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. RequestResponseAsync does not validate requestMessage.
        public async Task ThrowsArgumentNullExceptionWhenRequestMessageIsNull()
        {
            // RequestResponseAsync(FabricTransportMessage, TimeSpan, Guid) should validate the
            // requestMessage parameter and throw ArgumentNullException with paramName
            // "requestMessage" before constructing NativeFabricTransportMessage, whose
            // GetHeader/GetBody dereference the stored FabricTransportMessage reference.
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => sut.RequestResponseAsync(requestMessage: null, timeout));
            Assert.Equal(nameof(requestMessage), exception.ParamName);
        }

        [Fact]
        public async Task WrapsFabricCannotConnectExceptionAsConnectionDeniedWhenSecurityMismatch()
        {
            sut.Property<string>().Set("Secure");
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginRequest(
                    It.IsAny<IFabricTransportMessage>(),
                    (uint)timeout.TotalMilliseconds,
                    It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(new FabricCannotConnectException(fuzzy.String()));

            _ = await Assert.ThrowsAsync<FabricConnectionDeniedException>(() => sut.RequestResponseAsync(requestMessage, timeout));
        }

        [Fact]
        public async Task RethrowsFabricCannotConnectExceptionWhenNotSecurityMismatch()
        {
            sut.Property<string>().Set(fuzzy.Int64().ToString()); // Digits-only so it never contains the "Secure" marker checked by the SUT.
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginRequest(
                    It.IsAny<IFabricTransportMessage>(),
                    (uint)timeout.TotalMilliseconds,
                    It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(new FabricCannotConnectException(fuzzy.String()));

            _ = await Assert.ThrowsAsync<FabricCannotConnectException>(() => sut.RequestResponseAsync(requestMessage, timeout));
        }

        [Fact]
        public async Task RethrowsFabricCannotConnectExceptionWhenSecureAddressAndNonNoneCredentials()
        {
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { SecurityCredentials = new X509Credentials() });
            sut.Property<string>().Set("Secure");
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginRequest(
                    It.IsAny<IFabricTransportMessage>(),
                    (uint)timeout.TotalMilliseconds,
                    It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(new FabricCannotConnectException(fuzzy.String()));

            _ = await Assert.ThrowsAsync<FabricCannotConnectException>(() => sut.RequestResponseAsync(requestMessage, timeout));
        }

        [Fact(Explicit = true)] // TODO: SUT bug. IsSecurityMismatch dereferences SecurityCredentials without a null check.
        public async Task WrapsFabricCannotConnectExceptionAsConnectionDeniedWhenSecureAddressAndNullCredentials()
        {
            // Null SecurityCredentials is treated as no credentials everywhere else in the SUT
            // (FabricTransportSettings defaults SecurityCredentials to NoneSecurityCredentials and
            // FabricTransportSettingsExtension converts null as no native credentials), so a secure
            // address combined with null credentials is a security mismatch and should be wrapped
            // as FabricConnectionDeniedException. IsSecurityMismatch, however, dereferences
            // settings.SecurityCredentials.CredentialType without a null check and throws
            // NullReferenceException, so this test cannot pass against the current SUT.
            sut.Field<FabricTransportSettings>().Set(new FabricTransportSettings { SecurityCredentials = null });
            sut.Property<string>().Set("Secure");
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginRequest(
                    It.IsAny<IFabricTransportMessage>(),
                    (uint)timeout.TotalMilliseconds,
                    It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(new FabricCannotConnectException(fuzzy.String()));

            _ = await Assert.ThrowsAsync<FabricConnectionDeniedException>(() => sut.RequestResponseAsync(requestMessage, timeout));
        }

        [Fact]
        public async Task RethrowsOtherExceptions()
        {
            var expected = new InvalidOperationException(fuzzy.String());
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.BeginRequest(
                    It.IsAny<IFabricTransportMessage>(),
                    (uint)timeout.TotalMilliseconds,
                    It.IsAny<IFabricAsyncOperationCallback>()))
                .Throws(expected);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RequestResponseAsync(requestMessage, timeout));
            Assert.Same(expected, actual);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Utility wraps EndRequest exceptions; catch (TimeoutException) is unreachable.
        public void WrapsTimeoutExceptionWithErrorServiceTooBusy() =>
            // When the operation times out, RequestResponseAsync wraps TimeoutException with the
            // ErrorServiceTooBusy message. Empirically, throwing TimeoutException from a mocked
            // EndRequest (or EndRequestWithId) after invoking the captured callback surfaces as
            // System.Fabric.FabricException wrapping a System.Fabric.Interop.Utility+COMWrapperException
            // wrapping the original TimeoutException, because Utility.WrapNativeAsyncInvokeInMTA
            // translates exceptions through COM HResult mapping. The catch (TimeoutException)
            // branch in the SUT is therefore unreachable without substituting the interop helper,
            // which exposes no injection seam.
            throw new NotImplementedException();
    }

    public sealed class SendOneWay: FabricTransportClientTest
    {
        readonly FabricTransportMessage message = CreateMessage();

        [Fact]
        public void InvokesSendOnNativeClient()
        {
            var nativeClient = Mock.Of<IFabricTransportClient2>();
            sut.Field<IFabricTransportClient2>().Set(nativeClient);
            IFabricTransportMessage sent = null;
            _ = Mock.Get(nativeClient)
                .Setup(_ => _.Send(It.IsAny<IFabricTransportMessage>()))
                .Callback((IFabricTransportMessage actual) => sent = actual);

            sut.SendOneWay(message);

            Mock.Get(nativeClient).Verify(_ => _.Send(It.IsAny<IFabricTransportMessage>()), Times.Once);
            var wrapper = (NativeFabricTransportMessage)sent;
            Assert.Same(message, wrapper.Field<FabricTransportMessage>().Value);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. SendOneWay does not validate message.
        public void ThrowsArgumentNullExceptionWhenMessageIsNull()
        {
            // SendOneWay(FabricTransportMessage) should validate the message parameter and throw
            // ArgumentNullException with paramName "message" before constructing
            // NativeFabricTransportMessage, whose GetHeader/GetBody dereference the stored
            // FabricTransportMessage reference.
            sut.Field<IFabricTransportClient2>().Set(Mock.Of<IFabricTransportClient2>());
            var exception = Assert.Throws<ArgumentNullException>(() => sut.SendOneWay(message: null));
            Assert.Equal(nameof(message), exception.ParamName);
        }
    }

    public sealed class Settings: FabricTransportClientTest
    {
        [Fact]
        public void ReturnsAssignedSettings()
        {
            FabricTransportSettings expected = new();
            sut.Field<FabricTransportSettings>().Set(expected);
            Assert.Same(expected, sut.Settings);
        }
    }

    sealed class TestClient: FabricTransportClient
    {
    }

    static FabricTransportMessage CreateMessage() => new(
        new FabricTransportRequestHeader(new ArraySegment<byte>(fuzzy.Array(fuzzy.Byte)), () => { }),
        new FabricTransportRequestBody([new ArraySegment<byte>(fuzzy.Array(fuzzy.Byte))], () => { }));
}
