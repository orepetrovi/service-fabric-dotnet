// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    using System;

    /// <summary>
    /// Represents reliable state managed by an <see cref="IReliableStateManager"/>.
    /// </summary>
    public interface IReliableState
    {
        /// <summary>
        /// Gets a value indicating the unique name for the <see cref="IReliableState"/> instance.
        /// </summary>
        /// <value>
        /// The <see cref="System.Uri"/> name of this <see cref="IReliableState"/> instance.
        /// </value>
        Uri Name { get; }
    }
}
