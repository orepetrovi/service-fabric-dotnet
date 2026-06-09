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
    /// <remarks>
    /// More information on Reliable Collections can be seen
    /// <see href="https://docs.microsoft.com/azure/service-fabric/service-fabric-reliable-services-reliable-collections">here</see>.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    public interface IReliableCollection<T> : IReliableState
    {
        /// <summary>
        /// Returns the number of elements in the <see cref="IReliableCollection{T}"/>.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableCollection{T}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">The transaction has already been committed or aborted.</exception>
        /// <exception cref="FabricNotPrimaryException">
        /// The <see cref="IReliableCollection{T}"/> is not in the <see cref="ReplicaRole.Primary"/> role.
        /// In some instances, read operations, such as this one, can be performed from secondary replicas
        /// depending on the implementation of the <see cref="IReliableCollection{T}"/> used.
        /// </exception>
        // todo: the FabricNotPrimaryException <exception> text is internally contradictory - sentence 1 says it is thrown
        // when not Primary, while sentence 2 says reads such as this one can be served from secondary replicas; this
        // also overlaps with FabricNotReadableException above, which already documents the secondary-readable case. The
        // precise throw condition (and its relationship to FabricNotReadableException) cannot be verified from this
        // repository; rewrite the <exception> text once domain knowledge is available.
        Task<long> GetCountAsync(ITransaction tx);

        /// <summary>
        /// Removes all state from the <see cref="IReliableCollection{T}"/>, including replicated and persisted state.
        /// </summary>
        /// <exception cref="FabricNotPrimaryException">
        /// The <see cref="IReliableCollection{T}"/> is not in the <see cref="ReplicaRole.Primary"/> role.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// The operation failed to complete within the default timeout.
        /// </exception>
        // todo: ClearAsync has no timeout parameter, so the TimeoutException <exception> text's reference to "the given
        // timeout" is misleading; the actual timeout source (implicit default, configurable replicator setting, or none)
        // cannot be verified from this repository. Either rewrite the <exception> text to describe the actual default and
        // its source, or remove the <exception> element if it is unreachable from this signature, once domain knowledge
        // is available.
        Task ClearAsync();
    }
}
