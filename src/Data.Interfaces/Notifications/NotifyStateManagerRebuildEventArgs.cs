// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data.Notifications
{
    using Microsoft.ServiceFabric.Data;

    /// <summary>
    /// Provides data for the <see cref="IReliableStateManager.StateManagerChanged"/> event caused by a rebuild.
    /// Commonly called during recovery, restore and end of copy.
    /// </summary>
    public class NotifyStateManagerRebuildEventArgs : NotifyStateManagerChangedEventArgs
    {
        /// <summary>
        /// The state providers.
        /// </summary>
        private readonly IAsyncEnumerable<IReliableState> reliableStates;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotifyStateManagerRebuildEventArgs"/> class.
        /// </summary>
        /// <param name="reliableStates">
        /// An asynchronous sequence of <see cref="IReliableState"/> providers after the rebuild.
        /// </param>
        public NotifyStateManagerRebuildEventArgs(IAsyncEnumerable<IReliableState> reliableStates) : base(NotifyStateManagerChangedAction.Rebuild)
        {
            this.reliableStates = reliableStates;
        }

        /// <summary>
        /// Gets the new set of <see cref="IReliableState"/> providers now in the State Manager.
        /// </summary>
        public IAsyncEnumerable<IReliableState> ReliableStates
        {
            get
            {
                return this.reliableStates;
            }
        }
    }
}
