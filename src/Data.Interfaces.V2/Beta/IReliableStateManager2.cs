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
    /// Manages all <see cref="IReliableState"/> for a service replica.
    /// </summary>
    /// <remarks>
    /// (Beta) Not for production use - API is subject to change in the future.
    /// Each replica in a service has its own state manager and thus its own set of <see cref="IReliableState"/>.
    /// </remarks>
    public interface IReliableStateManager2 : IReliableStateManagerReplica2
    {
        /// <summary>
        /// Returns a new, started <see cref="ITransaction"/> that can be used to group operations to be performed atomically,
        /// using the specified isolation level for single-entity primary reads.
        /// </summary>
        /// <remarks>
        /// Operations are added to the transaction by passing the <see cref="ITransaction"/> object in to reliable state methods.
        /// </remarks>
        ITransaction CreateTransaction(IsolationLevel singleEntityIsolationLevelForPrimaryReads);
    }
}
