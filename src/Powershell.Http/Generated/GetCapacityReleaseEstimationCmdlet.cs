// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Powershell.Http
{
    using System;
    using System.Collections.Generic;
    using System.Management.Automation;
    using Microsoft.ServiceFabric.Common;

    /// <summary>
    /// Gets projected used capacity relative to cluster total capacity for each metric at each capacity release level.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "SFCapacityReleaseEstimation")]
    public partial class GetCapacityReleaseEstimationCmdlet : CommonCmdletBase
    {
        /// <summary>
        /// Gets or sets ContinuationToken. The continuation token to obtain the next set of results.
        /// </summary>
        [Parameter(Mandatory = false, Position = 0)]
        public ContinuationToken ContinuationToken { get; set; }

        /// <summary>
        /// Gets or sets MaxResults. The maximum number of results to return. The value must be non-negative.
        /// </summary>
        [Parameter(Mandatory = false, Position = 1)]
        [ValidateRange(0, long.MaxValue)]
        public long? MaxResults { get; set; }

        /// <summary>
        /// Gets or sets ServerTimeout. The server timeout for performing the operation in seconds. This timeout specifies the
        /// time duration that the client is willing to wait for the requested operation to complete. The default value for
        /// this parameter is 60 seconds.
        /// </summary>
        [Parameter(Mandatory = false, Position = 2)]
        public long? ServerTimeout { get; set; }

        /// <inheritdoc/>
        protected override void ProcessRecordInternal()
        {
            var result = this.ServiceFabricClient.Cluster.GetCapacityReleaseEstimationAsync(
                continuationToken: this.ContinuationToken,
                maxResults: this.MaxResults,
                serverTimeout: this.ServerTimeout,
                cancellationToken: this.CancellationToken).GetAwaiter().GetResult();

            if (result != null)
            {
                this.WriteObject(this.FormatOutput(result));
            }
        }

        /// <inheritdoc/>
        protected override object FormatOutput(object output)
        {
            return output;
        }
    }
}
