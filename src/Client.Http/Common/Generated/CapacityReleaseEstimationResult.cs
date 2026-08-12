// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The capacity release estimates and the recommended capacity release level based on those projections.
    /// </summary>
    public partial class CapacityReleaseEstimationResult
    {
        /// <summary>
        /// Initializes a new instance of the CapacityReleaseEstimationResult class.
        /// </summary>
        /// <param name="items">The estimates reporting projected used capacity relative to cluster total capacity for each
        /// cluster metric at each reported capacity release level.</param>
        /// <param name="recommendedLevel">The recommended capacity release level based on the projections. Possible values
        /// include: 'None', 'Minor', 'Major'
        /// 
        /// The level of capacity release applied to the cluster.
        /// </param>
        public CapacityReleaseEstimationResult(
            IEnumerable<CapacityReleaseEstimate> items,
            CapacityReleaseLevel? recommendedLevel)
        {
            items.ThrowIfNull(nameof(items));
            recommendedLevel.ThrowIfNull(nameof(recommendedLevel));
            this.Items = items;
            this.RecommendedLevel = recommendedLevel;
        }

        /// <summary>
        /// Gets the estimates reporting projected used capacity relative to cluster total capacity for each cluster metric at
        /// each reported capacity release level.
        /// </summary>
        public IEnumerable<CapacityReleaseEstimate> Items { get; }

        /// <summary>
        /// Gets the recommended capacity release level based on the projections. Possible values include: 'None', 'Minor',
        /// 'Major'
        /// 
        /// The level of capacity release applied to the cluster.
        /// </summary>
        public CapacityReleaseLevel? RecommendedLevel { get; }
    }
}
