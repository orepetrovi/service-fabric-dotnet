// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Services.Communication.Wcf
{
    using System.ServiceModel;

    internal static class WcfRemoteExceptionInformation
    {
        public static readonly string FaultCodeName = "WcfRemoteExceptionInformation";
        public static readonly string FaultSubCodeRetryName = "Retry";

        public static readonly FaultCode FaultCodeRetry = new FaultCode(
            FaultCodeName,
            new FaultCode(FaultSubCodeRetryName));
    }
}
