// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Microsoft.ServiceFabric.Data.Beta
{
        
    /// <summary>
    /// Defines isolation levels for single-item primary reads within a transaction.
    /// </summary>
    /// <remarks>
    /// This setting governs only single-entity reads on the primary. Multi-entity reads, such as count and enumeration,
    /// and all reads on secondary replicas always use <see cref="Snapshot"/> regardless of this setting.
    /// </remarks>
    public enum IsolationLevel
    {
        /// <summary>
        /// Holds read locks on the entities read on the primary until the transaction completes, preventing concurrent
        /// modification of those entities. This is the default.
        /// </summary>
        ReadRepeatable = 0,

        /// <summary>
        /// Reads each entity from a consistent snapshot taken when the transaction started, without acquiring read locks.
        /// </summary>
        Snapshot = 1
    }
    
}
