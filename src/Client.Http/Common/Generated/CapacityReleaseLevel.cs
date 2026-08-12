// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    /// <summary>
    /// Defines values for CapacityReleaseLevel.
    /// </summary>
    public enum CapacityReleaseLevel
    {
        /// <summary>
        /// Indicates no capacity release.
        /// </summary>
        None,

        /// <summary>
        /// Indicates minor capacity release.
        /// </summary>
        Minor,

        /// <summary>
        /// Indicates major capacity release.
        /// </summary>
        Major,
    }
}
