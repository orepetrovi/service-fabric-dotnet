// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data.Collections
{
    /// <summary>
    /// Specifies how reliable collections will lock resources, which determines
    /// how the resources can be accessed by concurrent transactions.
    /// </summary>
    public enum LockMode : int
    {
        /// <summary>
        /// Selects the default lock mode based on the operation and isolation level of the transaction.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Acquires an update-intent lock on resources the transaction intends to update later, preventing a common
        /// form of deadlock that occurs when multiple transactions read, lock, and then attempt to update the same resources.
        /// </summary>
        Update = 1,
    }
}
