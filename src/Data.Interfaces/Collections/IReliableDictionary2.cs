// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data.Collections
{
    using System;
    using System.Fabric;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a Reliable Collection of key/value pairs that are persisted and replicated.
    /// </summary>
    /// <inheritdoc cref="IReliableDictionary{TKey,TValue}" path="/remarks"/>
    public interface IReliableDictionary2<TKey, TValue> : IReliableDictionary<TKey, TValue>
        where TKey : IComparable<TKey>, IEquatable<TKey>
    {
        /// <summary>
        /// Asynchronously creates an enumerable over the keys of the <see cref="IReliableDictionary2{TKey,TValue}"/>.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <exception cref="ArgumentNullException"><paramref name="txn"/> is <see langword="null"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The Reliable Dictionary cannot serve reads at the moment.
        /// <see cref="FabricNotReadableException"/> can be thrown in all <see cref="ReplicaRole"/>s.
        /// One example for it being thrown in the <see cref="ReplicaRole.Primary"/> is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One example for it being thrown in the <see cref="ReplicaRole.ActiveSecondary"/> is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <remarks>
        /// The enumerable returned from the Reliable Dictionary is safe to use concurrently with reads and writes
        /// to the dictionary. It represents a snapshot consistent view of the dictionary. Keys are always enumerated in ordered mode.
        /// </remarks>
        Task<IAsyncEnumerable<TKey>> CreateKeyEnumerableAsync(ITransaction txn);

        /// <inheritdoc cref="CreateKeyEnumerableAsync(ITransaction)" path="/summary"/>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <param name="enumerationMode">An ignored enumeration mode. Results are always returned in ordered mode.</param>
        /// <inheritdoc cref="CreateKeyEnumerableAsync(ITransaction)" path="/exception"/>
        /// <inheritdoc cref="CreateKeyEnumerableAsync(ITransaction)" path="/remarks"/>
        Task<IAsyncEnumerable<TKey>> CreateKeyEnumerableAsync(ITransaction txn, EnumerationMode enumerationMode);

        /// <inheritdoc cref="CreateKeyEnumerableAsync(ITransaction)" path="/summary"/>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <param name="enumerationMode">An ignored enumeration mode. Results are always returned in ordered mode.</param>
        /// <param name="timeout">An ignored timeout.</param>
        /// <param name="cancellationToken">An ignored cancellation token.</param>
        /// <inheritdoc cref="CreateKeyEnumerableAsync(ITransaction)" path="/exception"/>
        /// <inheritdoc cref="CreateKeyEnumerableAsync(ITransaction, EnumerationMode)" path="/remarks"/>
        Task<IAsyncEnumerable<TKey>> CreateKeyEnumerableAsync(
            ITransaction txn, 
            EnumerationMode enumerationMode,
            TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets the number of key/value pairs contained in the <see cref="IReliableDictionary2{TKey,TValue}"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">A property access is invalid for the object's current state.</exception>
        /// <remarks>
        /// This property does not have transactional semantics. It represents the best effort number of items 
        /// in the dictionary at the moment when the property was accessed.
        /// </remarks>
        long Count { get; }
    }
}
