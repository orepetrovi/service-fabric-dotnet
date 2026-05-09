// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Services.Communication.Wcf
{
    using System.ServiceModel;

    internal static class WcfRemoteExceptionInformation
    {
        internal static readonly string FaultCodeName = "WcfRemoteExceptionInformation";
        internal static readonly string FaultSubCodeRetryName = "Retry";

        internal static readonly FaultCode FaultCodeRetry = new FaultCode(
            FaultCodeName,
            new FaultCode(FaultSubCodeRetryName));
    }
}
