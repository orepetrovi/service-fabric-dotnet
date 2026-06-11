// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data.Collections
{
    using System;
    using System.Fabric;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a Reliable Collection of elements of type <typeparamref name="T"/>.
    /// </summary>
    /// <seealso href="https://learn.microsoft.com/azure/service-fabric/service-fabric-reliable-services-reliable-collections">Reliable Collections</seealso>
    public interface IReliableCollection<T> : IReliableState
    {
        /// <summary>
        /// Asynchronously returns the number of elements in the <see cref="IReliableCollection{T}"/>.
        /// </summary>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableCollection{T}"/> cannot serve reads.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableCollection{T}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">The transaction has already been committed or aborted.</exception>
        /// <exception cref="FabricNotPrimaryException">
        /// The <see cref="IReliableCollection{T}"/> is not in the <see cref="ReplicaRole.Primary"/> role.
        /// In some instances, read operations, such as this one, can be performed from secondary replicas
        /// depending on the implementation of the <see cref="IReliableCollection{T}"/> used.
        /// </exception>
        Task<long> GetCountAsync(ITransaction tx);

        /// <summary>
        /// Asynchronously removes all state from the <see cref="IReliableCollection{T}"/>, including replicated and persisted state.
        /// </summary>
        /// <remarks>
        /// Not every <see cref="IReliableCollection{T}"/> implementation supports clearing.
        /// </remarks>
        /// <exception cref="FabricNotPrimaryException">
        /// The <see cref="IReliableCollection{T}"/> is not in the <see cref="ReplicaRole.Primary"/> role.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// The operation failed to complete within the default timeout.
        /// </exception>
        Task ClearAsync();
    }
}
