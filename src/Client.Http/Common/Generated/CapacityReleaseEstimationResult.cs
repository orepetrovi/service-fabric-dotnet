// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A page of capacity release estimates.
    /// </summary>
    public partial class CapacityReleaseEstimationResult
    {
        /// <summary>
        /// Initializes a new instance of the CapacityReleaseEstimationResult class.
        /// </summary>
        /// <param name="items">The estimates reporting projected used capacity relative to cluster total capacity for each
        /// cluster metric at each reported capacity release level.</param>
        /// <param name="continuationToken">The continuation token for the next page of results.</param>
        public CapacityReleaseEstimationResult(
            IEnumerable<CapacityReleaseEstimate> items,
            ContinuationToken continuationToken = default(ContinuationToken))
        {
            items.ThrowIfNull(nameof(items));
            this.Items = items;
            this.ContinuationToken = continuationToken;
        }

        /// <summary>
        /// Gets the estimates reporting projected used capacity relative to cluster total capacity for each cluster metric at
        /// each reported capacity release level.
        /// </summary>
        public IEnumerable<CapacityReleaseEstimate> Items { get; }

        /// <summary>
        /// Gets the continuation token for the next page of results.
        /// </summary>
        public ContinuationToken ContinuationToken { get; }
    }
}
