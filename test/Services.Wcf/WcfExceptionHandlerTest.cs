// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Services.Communication.Wcf.Client;

using System;
using System.ServiceModel;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Xunit;

public abstract class WcfExceptionHandlerTest
{
    readonly IExceptionHandler sut = new WcfExceptionHandler();

    // Constructor parameters
    readonly OperationRetrySettings retrySettings = new();

    public sealed class TryHandleException : WcfExceptionHandlerTest
    {
        [Fact]
        public void ReturnsTrueWithRetryResultForWellKnownFaultException()
        {
            var fe = new FaultException(
                new FaultReason("System.InvalidOperationException"),
                WcfRemoteExceptionInformation.FaultCodeRetry);

            bool handled = sut.TryHandleException(
                new ExceptionInformation(fe),
                retrySettings,
                out var result);

            Assert.True(handled);
            var retry = Assert.IsType<ExceptionHandlingRetryResult>(result);
            Assert.False(retry.IsTransient);
            Assert.Equal(retrySettings.DefaultMaxRetryCountForNonTransientErrors, retry.MaxRetryCount);
            Assert.Equal(fe.Reason.ToString(), retry.ExceptionId);
        }

        [Fact]
        public void DoesNotDeserializeHostilePayloadInFaultReason()
        {
            HostileSentinel.WasInstantiated = false;

            string hostileXml = "<HostileSentinel xmlns=\"http://schemas.datacontract.org/2004/07/" +
                typeof(HostileSentinel).Namespace + "\" />";
            var fe = new FaultException(
                new FaultReason(hostileXml),
                WcfRemoteExceptionInformation.FaultCodeRetry);

            sut.TryHandleException(
                new ExceptionInformation(fe),
                retrySettings,
                out _);

            Assert.False(HostileSentinel.WasInstantiated);
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
                out var result);

            Assert.False(handled);
            Assert.Null(result);
        }
    }

    [Serializable]
    sealed class HostileSentinel : Exception
    {
        internal static bool WasInstantiated;

        public HostileSentinel()
        {
            WasInstantiated = true;
        }
    }
}
