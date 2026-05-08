// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Services.Communication.Wcf.Client;

using System;
using System.ServiceModel;
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
        readonly OperationRetrySettings retrySettings = new(
            fuzzy.TimeSpan(),
            fuzzy.TimeSpan(),
            fuzzy.Int32().Between(1, 10),
            fuzzy.Int32().Between(1, 10));

        [Fact]
        public void ReturnsTrueWithRetryResultForWellKnownFaultException()
        {
            var fe = new FaultException(
                new FaultReason("System.InvalidOperationException"),
                WcfRemoteExceptionInformation.FaultCodeRetry);

            bool handled = sut.TryHandleException(
                new ExceptionInformation(fe),
                retrySettings,
                out ExceptionHandlingResult result);

            Assert.True(handled);
            ExceptionHandlingRetryResult retry = Assert.IsType<ExceptionHandlingRetryResult>(result);
            Assert.False(retry.IsTransient);
            Assert.Equal(retrySettings.DefaultMaxRetryCountForNonTransientErrors, retry.MaxRetryCount);
            Assert.Equal(fe.Reason.ToString(), retry.ExceptionId);
        }

        [Fact]
        public void ReturnsFalseForFaultExceptionWithoutRetrySubCode()
        {
            var fe = new FaultException(
                new FaultReason("System.InvalidOperationException"),
                new FaultCode(WcfRemoteExceptionInformation.FaultCodeName));

            bool handled = sut.TryHandleException(
                new ExceptionInformation(fe),
                retrySettings,
                out ExceptionHandlingResult result);

            Assert.False(handled);
            Assert.Null(result);
        }
    }
}
