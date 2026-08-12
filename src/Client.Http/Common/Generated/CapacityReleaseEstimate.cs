// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An estimate of the capacity available at a capacity release level for a cluster metric.
    /// </summary>
    public partial class CapacityReleaseEstimate
    {
        /// <summary>
        /// Initializes a new instance of the CapacityReleaseEstimate class.
        /// </summary>
        /// <param name="level">The capacity release level represented by this estimate. Possible values include: 'None',
        /// 'Minor', 'Major'
        /// 
        /// The level of capacity release applied to the cluster.
        /// </param>
        /// <param name="metricName">The name of the cluster metric.</param>
        /// <param name="usedCapacity">The capacity currently used for the metric.</param>
        /// <param name="totalCapacity">The total capacity available for the metric.</param>
        public CapacityReleaseEstimate(
            CapacityReleaseLevel? level,
            string metricName,
            long? usedCapacity,
            long? totalCapacity)
        {
            level.ThrowIfNull(nameof(level));
            metricName.ThrowIfNull(nameof(metricName));
            usedCapacity.ThrowIfNull(nameof(usedCapacity));
            totalCapacity.ThrowIfNull(nameof(totalCapacity));
            this.Level = level;
            this.MetricName = metricName;
            this.UsedCapacity = usedCapacity;
            this.TotalCapacity = totalCapacity;
        }

        /// <summary>
        /// Gets the capacity release level represented by this estimate. Possible values include: 'None', 'Minor', 'Major'
        /// 
        /// The level of capacity release applied to the cluster.
        /// </summary>
        public CapacityReleaseLevel? Level { get; }

        /// <summary>
        /// Gets the name of the cluster metric.
        /// </summary>
        public string MetricName { get; }

        /// <summary>
        /// Gets the capacity currently used for the metric.
        /// </summary>
        public long? UsedCapacity { get; }

        /// <summary>
        /// Gets the total capacity available for the metric.
        /// </summary>
        public long? TotalCapacity { get; }
    }
}
