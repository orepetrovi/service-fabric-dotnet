// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Services.Communication.Wcf.Client;

using System;
using System.ServiceModel;
using System.ServiceModel.Security;
using Fuzzy;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Xunit;

public abstract class WcfExceptionHandlerTest
{
    readonly IExceptionHandler sut = new WcfExceptionHandler();

    // Test fixture
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    public sealed class TryHandleException : WcfExceptionHandlerTest
    {
        readonly OperationRetrySettings retrySettings =
            new(fuzzy.TimeSpan(), fuzzy.TimeSpan(), fuzzy.Int32().Between(1, 10), fuzzy.Int32().Between(1, 10));

        void AssertNonTransientRetry(Exception e)
        {
            bool handled = sut.TryHandleException(new ExceptionInformation(e), retrySettings, out ExceptionHandlingResult result);
            Assert.True(handled);
            ExceptionHandlingRetryResult retry = Assert.IsType<ExceptionHandlingRetryResult>(result);
            Assert.False(retry.IsTransient);
            Assert.Equal(retrySettings.DefaultMaxRetryCountForNonTransientErrors, retry.MaxRetryCount);
        }

        [Fact]
        public void ReturnsNonTransientRetryForEndpointNotFoundException() =>
            AssertNonTransientRetry(new EndpointNotFoundException(fuzzy.String()));

        [Fact]
        public void ReturnsNonTransientRetryForCommunicationObjectAbortedException() =>
            AssertNonTransientRetry(new CommunicationObjectAbortedException(fuzzy.String()));

        [Fact]
        public void ReturnsNonTransientRetryForCommunicationObjectFaultedException() =>
            AssertNonTransientRetry(new CommunicationObjectFaultedException(fuzzy.String()));

        [Fact]
        public void ReturnsNonTransientRetryForObjectDisposedException() =>
            AssertNonTransientRetry(new ObjectDisposedException(fuzzy.String()));

        [Fact]
        public void ReturnsNonTransientRetryForChannelTerminatedException() =>
            AssertNonTransientRetry(new ChannelTerminatedException(fuzzy.String()));

        [Fact]
        public void ReturnsTransientRetryForTimeoutException()
        {
            var e = new TimeoutException(fuzzy.String());
            bool handled = sut.TryHandleException(new ExceptionInformation(e), retrySettings, out ExceptionHandlingResult result);
            Assert.True(handled);
            ExceptionHandlingRetryResult retry = Assert.IsType<ExceptionHandlingRetryResult>(result);
            Assert.True(retry.IsTransient);
            Assert.Equal(int.MaxValue, retry.MaxRetryCount);
        }

        [Fact]
        public void ReturnsTransientRetryForServerTooBusyException()
        {
            var e = new ServerTooBusyException(fuzzy.String());
            bool handled = sut.TryHandleException(new ExceptionInformation(e), retrySettings, out ExceptionHandlingResult result);
            Assert.True(handled);
            ExceptionHandlingRetryResult retry = Assert.IsType<ExceptionHandlingRetryResult>(result);
            Assert.True(retry.IsTransient);
            Assert.Equal(int.MaxValue, retry.MaxRetryCount);
        }

        [Fact]
        public void ReturnsThrowResultForActionNotSupportedException()
        {
            var e = new ActionNotSupportedException(fuzzy.String());
            bool handled = sut.TryHandleException(new ExceptionInformation(e), retrySettings, out ExceptionHandlingResult result);
            Assert.True(handled);
            ExceptionHandlingThrowResult throwResult = Assert.IsType<ExceptionHandlingThrowResult>(result);
            Assert.Same(e, throwResult.ExceptionToThrow);
        }

        [Fact]
        public void ReturnsThrowResultForAddressAccessDeniedException()
        {
            var e = new AddressAccessDeniedException(fuzzy.String());
            bool handled = sut.TryHandleException(new ExceptionInformation(e), retrySettings, out ExceptionHandlingResult result);
            Assert.True(handled);
            ExceptionHandlingThrowResult throwResult = Assert.IsType<ExceptionHandlingThrowResult>(result);
            Assert.Same(e, throwResult.ExceptionToThrow);
        }

        [Fact]
        public void ReturnsThrowResultForSecurityAccessDeniedException()
        {
            var e = new SecurityAccessDeniedException(fuzzy.String());
            bool handled = sut.TryHandleException(new ExceptionInformation(e), retrySettings, out ExceptionHandlingResult result);
            Assert.True(handled);
            ExceptionHandlingThrowResult throwResult = Assert.IsType<ExceptionHandlingThrowResult>(result);
            Assert.Same(e, throwResult.ExceptionToThrow);
        }

        [Fact]
        public void ReturnsRetryResultForWellKnownFaultException()
        {
            var fe = new FaultException(new FaultReason(typeof(InvalidOperationException).FullName), WcfRemoteExceptionInformation.FaultCodeRetry);

            bool handled = sut.TryHandleException(new ExceptionInformation(fe), retrySettings, out ExceptionHandlingResult result);

            Assert.True(handled);
            ExceptionHandlingRetryResult retry = Assert.IsType<ExceptionHandlingRetryResult>(result);
            Assert.False(retry.IsTransient);
            Assert.Equal(retrySettings.DefaultMaxRetryCountForNonTransientErrors, retry.MaxRetryCount);
            Assert.Equal(fe.Reason.ToString(), retry.ExceptionId);
        }

        [Fact]
        public void ReturnsNonTransientRetryForGenericCommunicationException()
        {
            var e = new CommunicationException(fuzzy.String());

            bool handled = sut.TryHandleException(new ExceptionInformation(e), retrySettings, out ExceptionHandlingResult result);

            Assert.True(handled);
            ExceptionHandlingRetryResult retry = Assert.IsType<ExceptionHandlingRetryResult>(result);
            Assert.False(retry.IsTransient);
            Assert.Equal(retrySettings.DefaultMaxRetryCountForNonTransientErrors, retry.MaxRetryCount);
        }

        [Fact]
        public void ReturnsFalseForFaultExceptionWithoutRetrySubCode()
        {
            var fe = new FaultException(new FaultReason(fuzzy.String()), new FaultCode(WcfRemoteExceptionInformation.FaultCodeName));

            bool handled = sut.TryHandleException(new ExceptionInformation(fe), retrySettings, out ExceptionHandlingResult result);

            Assert.False(handled);
            Assert.Null(result);
        }

        [Fact]
        public void ReturnsFalseForPlainFaultException()
        {
            var fe = new FaultException(fuzzy.String());

            bool handled = sut.TryHandleException(new ExceptionInformation(fe), retrySettings, out ExceptionHandlingResult result);

            Assert.False(handled);
            Assert.Null(result);
        }
    }
}
