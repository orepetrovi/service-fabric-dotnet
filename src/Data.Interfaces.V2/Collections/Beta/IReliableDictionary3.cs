// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data.Collections.Beta
{
    using System;
    using System.Fabric;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// (Beta) Not for production use - API is subject to change in the future.
    /// Represents a reliable collection of key/value pairs that are persisted and replicated.
    /// </summary>
    /// <remarks>Keys or values stored in this dictionary MUST NOT be mutated outside the context of an operation on the
    /// dictionary.  It is highly recommended to make both <typeparamref name="TKey"/> and <typeparamref name="TValue"/>
    /// immutable in order to avoid accidental data corruption.
    ///
    /// <para>
    /// The transaction is the unit of concurrency. Users can have multiple transactions in-flight at any given point of time, but for a given transaction each API must be called one at a time.
    /// When calling any asynchronous Reliable Collection method that takes an <see cref="ITransaction"/>, you must wait for completion of the returned Task before calling
    /// another method using the same transaction.
    /// </para>
    /// </remarks>
    public interface IReliableDictionary3<TKey, TValue> : IReliableDictionary2<TKey, TValue>
        where TKey : IComparable<TKey>, IEquatable<TKey>
    {
        /// <summary>
        /// (Beta) Asynchronously attempts to get the sequence number associated with the specified key from the Reliable Dictionary.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element whose sequence number is to be retrieved.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <returns>A result indicating whether the key was found in the Reliable Dictionary and, if so, its sequence number.</returns>
        Task<ConditionalValue<long>> TryGetSequenceNumberAsync(
            ITransaction tx,
            TKey key);

        /// <summary>
        /// (Beta) Asynchronously attempts to get the sequence number associated with the specified key from the Reliable Dictionary.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element whose sequence number is to be retrieved.</param>
        /// <param name="lockMode">The type of locking to use for this read operation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="lockMode"/> is not a valid <see cref="LockMode"/> value.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <returns>A result indicating whether the key was found in the Reliable Dictionary and, if so, its sequence number.</returns>
        Task<ConditionalValue<long>> TryGetSequenceNumberAsync(
            ITransaction tx,
            TKey key,
            LockMode lockMode);

        /// <summary>
        /// (Beta) Asynchronously attempts to get the sequence number associated with the specified key from the Reliable Dictionary.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element whose sequence number is to be retrieved.</param>
        /// <param name="lockMode">The type of locking to use for this read operation.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a TimeoutException. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative, or <paramref name="lockMode"/> is not a valid <see cref="LockMode"/> value.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <returns>A result indicating whether the key was found in the Reliable Dictionary and, if so, its sequence number.</returns>
        Task<ConditionalValue<long>> TryGetSequenceNumberAsync(
            ITransaction tx,
            TKey key,
            LockMode lockMode,
            TimeSpan timeout,
            CancellationToken cancellationToken);


        /// <summary>
        /// (Beta) Asynchronously attempts to get the versioned element associated with the specified key from the Reliable Dictionary.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the versioned element to get.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <returns>A result indicating whether the key was found in the Reliable Dictionary and, if so, its value and sequence number.</returns>
        Task<ConditionalValue<VersionedKeyValuePair<TKey, TValue>>> TryGetVersionedKeyValuePairAsync(
            ITransaction tx,
            TKey key);

        /// <summary>
        /// (Beta) Asynchronously attempts to get the versioned element associated with the specified key from the Reliable Dictionary.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the versioned element to get.</param>
        /// <param name="lockMode">The type of locking to use for this read operation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="lockMode"/> is not a valid <see cref="LockMode"/> value.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <returns>A result indicating whether the key was found in the Reliable Dictionary and, if so, its value and sequence number.</returns>
        Task<ConditionalValue<VersionedKeyValuePair<TKey, TValue>>> TryGetVersionedKeyValuePairAsync(
            ITransaction tx,
            TKey key,
            LockMode lockMode);

        /// <summary>
        /// (Beta) Asynchronously attempts to get the versioned element associated with the specified key from the Reliable Dictionary.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the versioned element to get.</param>
        /// <param name="lockMode">The type of locking to use for this read operation.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a TimeoutException. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative, or <paramref name="lockMode"/> is not a valid <see cref="LockMode"/> value.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <returns>A result indicating whether the key was found in the Reliable Dictionary and, if so, its value and sequence number.</returns>
        Task<ConditionalValue<VersionedKeyValuePair<TKey, TValue>>> TryGetVersionedKeyValuePairAsync(
            ITransaction tx,
            TKey key,
            LockMode lockMode,
            TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// (Beta) Asynchronously attempts to update the value for the specified key given the sequence number is matching.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to be updated.</param>
        /// <param name="newValue">The value to be updated to if the specified <paramref name="key"/> has the expected <paramref name="checkSequenceNumber"/>.</param>
        /// <param name="checkSequenceNumber">The expected sequence number of the element to be updated.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary3{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <returns><see langword="true"/> if the value was updated; otherwise, <see langword="false"/>.</returns>
        Task<bool> TryUpdateAsync(ITransaction tx, TKey key, TValue newValue, long checkSequenceNumber);

        /// <summary>
        /// (Beta) Asynchronously attempts to update the value for the specified key given the sequence number is matching.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to be updated.</param>
        /// <param name="newValue">The value to be updated to if the specified <paramref name="key"/> has the expected <paramref name="checkSequenceNumber"/>.</param>
        /// <param name="checkSequenceNumber">The expected sequence number of the element to be updated.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a TimeoutException. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary3{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <returns><see langword="true"/> if the value was updated; otherwise, <see langword="false"/>.</returns>
        Task<bool> TryUpdateAsync(ITransaction tx, TKey key, TValue newValue, long checkSequenceNumber, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// (Beta) Asynchronously attempts to remove the value with the specified key given the sequence number is matching.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="checkSequenceNumber">The expected sequence number of the element to be removed.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary3{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <returns>
        /// <see langword="true"/> if the element with the matching sequence number was removed; otherwise, <see langword="false"/>.
        /// </returns>
        Task<bool> TryRemoveAsync(ITransaction tx, TKey key, long checkSequenceNumber);

        /// <summary>
        /// (Beta) Asynchronously attempts to remove the value with the specified key given the sequence number is matching.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="checkSequenceNumber">The expected sequence number of the element to be removed.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a TimeoutException. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary3{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <returns>
        /// <see langword="true"/> if the element with the matching sequence number was removed; otherwise, <see langword="false"/>.
        /// </returns>
        Task<bool> TryRemoveAsync(ITransaction tx, TKey key, long checkSequenceNumber, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// (Beta) Asynchronously creates an enumerable over the <see cref="IReliableDictionary3{TKey, TValue}"/> to enumerate the versioned keys.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <exception cref="ArgumentNullException"><paramref name="txn"/> is <see langword="null"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <remarks>The returned enumerable is safe to use concurrently with reads and writes to the Reliable Dictionary.
        /// It represents a snapshot consistent view.</remarks>
        /// <returns>An enumerable for the <see cref="IReliableDictionary3{TKey, TValue}"/> versioned keys.</returns>
        Task<IAsyncEnumerable<VersionedKey<TKey>>> CreateVersionedKeyEnumerableAsync(ITransaction txn);

        /// <summary>
        /// (Beta) Asynchronously creates an enumerable over the <see cref="IReliableDictionary3{TKey, TValue}"/> to enumerate the versioned keys.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <param name="firstKey">The key to start enumerating from in ordered enumeration.</param>
        /// <exception cref="ArgumentNullException"><paramref name="txn"/> or <paramref name="firstKey"/> is <see langword="null"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <remarks>The returned enumerable is safe to use concurrently with reads and writes to the Reliable Dictionary.
        /// It represents a snapshot consistent view.</remarks>
        /// <returns>An enumerable for the <see cref="IReliableDictionary3{TKey, TValue}"/> versioned keys.</returns>
        Task<IAsyncEnumerable<VersionedKey<TKey>>> CreateVersionedKeyEnumerableAsync(ITransaction txn, TKey firstKey);

        /// <summary>
        /// (Beta) Asynchronously creates an enumerable over the <see cref="IReliableDictionary3{TKey, TValue}"/> to enumerate the versioned keys.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <param name="firstKey">The key to start enumerating from in ordered enumeration.</param>
        /// <param name="lastKey">The key to stop enumerating at in ordered enumeration.</param>
        /// <exception cref="ArgumentNullException"><paramref name="txn"/>, <paramref name="firstKey"/>, or <paramref name="lastKey"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="firstKey"/> is greater than <paramref name="lastKey"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <remarks>The returned enumerable is safe to use concurrently with reads and writes to the Reliable Dictionary.
        /// It represents a snapshot consistent view.</remarks>
        /// <returns>An enumerable for the <see cref="IReliableDictionary3{TKey, TValue}"/> versioned keys.</returns>
        Task<IAsyncEnumerable<VersionedKey<TKey>>> CreateVersionedKeyEnumerableAsync(ITransaction txn, TKey firstKey, TKey lastKey);

        /// <summary>
        /// (Beta) Asynchronously creates an enumerable over the <see cref="IReliableDictionary3{TKey, TValue}"/> to enumerate the versioned key/value pairs.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <exception cref="ArgumentNullException"><paramref name="txn"/> is <see langword="null"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <remarks>The returned enumerable is safe to use concurrently with reads and writes to the Reliable Dictionary.
        /// It represents a snapshot consistent view. Example usage can be
        /// seen <see href="https://github.com/Azure-Samples/service-fabric-dotnet-web-reference-app/blob/master/ReferenceApp/Inventory.Service/InventoryService.cs">here</see>.</remarks>
        /// <returns>An enumerable for the <see cref="IReliableDictionary3{TKey, TValue}"/> versioned key/value pairs.</returns>
        Task<IAsyncEnumerable<VersionedKeyValuePair<TKey, TValue>>> CreateVersionedEnumerableAsync(ITransaction txn);

        /// <summary>
        /// (Beta) Asynchronously creates an enumerable over the <see cref="IReliableDictionary3{TKey, TValue}"/> to enumerate the versioned key/value pairs.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <param name="firstKey">The key to start enumerating from in ordered enumeration.</param>
        /// <exception cref="ArgumentNullException"><paramref name="txn"/> or <paramref name="firstKey"/> is <see langword="null"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <remarks>The returned enumerable is safe to use concurrently with reads and writes to the Reliable Dictionary.
        /// It represents a snapshot consistent view. Example usage can be
        /// seen <see href="https://github.com/Azure-Samples/service-fabric-dotnet-web-reference-app/blob/master/ReferenceApp/Inventory.Service/InventoryService.cs">here</see>.</remarks>
        /// <returns>An enumerable for the <see cref="IReliableDictionary3{TKey, TValue}"/> versioned key/value pairs.</returns>
        Task<IAsyncEnumerable<VersionedKeyValuePair<TKey, TValue>>> CreateVersionedEnumerableAsync(ITransaction txn, TKey firstKey);

        /// <summary>
        /// (Beta) Asynchronously creates an enumerable over the <see cref="IReliableDictionary3{TKey, TValue}"/> to enumerate the versioned key/value pairs.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <param name="firstKey">The key to start enumerating from in ordered enumeration.</param>
        /// <param name="lastKey">The key to stop enumerating at in ordered enumeration.</param>
        /// <exception cref="ArgumentNullException"><paramref name="txn"/>, <paramref name="firstKey"/>, or <paramref name="lastKey"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="firstKey"/> is greater than <paramref name="lastKey"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <remarks>The returned enumerable is safe to use concurrently with reads and writes to the Reliable Dictionary.
        /// It represents a snapshot consistent view. Example usage can be
        /// seen <see href="https://github.com/Azure-Samples/service-fabric-dotnet-web-reference-app/blob/master/ReferenceApp/Inventory.Service/InventoryService.cs">here</see>.</remarks>
        /// <returns>An enumerable for the <see cref="IReliableDictionary3{TKey, TValue}"/> versioned key/value pairs.</returns>
        Task<IAsyncEnumerable<VersionedKeyValuePair<TKey, TValue>>> CreateVersionedEnumerableAsync(ITransaction txn, TKey firstKey, TKey lastKey);

        /// <summary>
        /// (Beta) Asynchronously creates an enumerable over the <see cref="IReliableDictionary3{TKey, TValue}"/> to enumerate the versioned key/value pairs.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <param name="filter">The predicate that filters the versioned key/value pairs to include in the enumeration based on the key, or <see langword="null"/> to include all versioned key/value pairs.</param>
        /// <exception cref="ArgumentNullException"><paramref name="txn"/> is <see langword="null"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <remarks>The returned enumerable is safe to use concurrently with reads and writes to the Reliable Dictionary.
        /// It represents a snapshot consistent view. Example usage can be
        /// seen <see href="https://github.com/Azure-Samples/service-fabric-dotnet-web-reference-app/blob/master/ReferenceApp/Inventory.Service/InventoryService.cs">here</see>.</remarks>
        /// <returns>An enumerable for the <see cref="IReliableDictionary3{TKey, TValue}"/> versioned key/value pairs.</returns>
        Task<IAsyncEnumerable<VersionedKeyValuePair<TKey, TValue>>> CreateVersionedEnumerableAsync(ITransaction txn, Func<TKey, bool> filter);

        /// <summary>
        /// (Beta) Asynchronously creates an enumerable over the <see cref="IReliableDictionary3{TKey, TValue}"/> to enumerate the versioned key/value pairs.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <param name="filter">The predicate that filters the versioned key/value pairs to include in the enumeration based on the key, or <see langword="null"/> to include all versioned key/value pairs.</param>
        /// <param name="firstKey">The key to start enumerating from in ordered enumeration.</param>
        /// <exception cref="ArgumentNullException"><paramref name="txn"/> or <paramref name="firstKey"/> is <see langword="null"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <remarks>The returned enumerable is safe to use concurrently with reads and writes to the Reliable Dictionary.
        /// It represents a snapshot consistent view. Example usage can be
        /// seen <see href="https://github.com/Azure-Samples/service-fabric-dotnet-web-reference-app/blob/master/ReferenceApp/Inventory.Service/InventoryService.cs">here</see>.</remarks>
        /// <returns>An enumerable for the <see cref="IReliableDictionary3{TKey, TValue}"/> versioned key/value pairs.</returns>
        Task<IAsyncEnumerable<VersionedKeyValuePair<TKey, TValue>>> CreateVersionedEnumerableAsync(ITransaction txn, Func<TKey, bool> filter, TKey firstKey);

        /// <summary>
        /// (Beta) Asynchronously creates an enumerable over the <see cref="IReliableDictionary3{TKey, TValue}"/> to enumerate the versioned key/value pairs.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <param name="filter">The predicate that filters the versioned key/value pairs to include in the enumeration based on the key, or <see langword="null"/> to include all versioned key/value pairs.</param>
        /// <param name="firstKey">The key to start enumerating from in ordered enumeration.</param>
        /// <param name="lastKey">The key to stop enumerating at in ordered enumeration.</param>
        /// <exception cref="ArgumentNullException"><paramref name="txn"/>, <paramref name="firstKey"/>, or <paramref name="lastKey"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="firstKey"/> is greater than <paramref name="lastKey"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary3{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that Reliable Collection's state is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// Example, transaction used is already terminated: committed or aborted by the user.
        /// If this exception is thrown, it is highly likely that there is a bug in the service code of the use of transactions.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">The Reliable Dictionary is closed or deleted.</exception>
        /// <remarks>The returned enumerable is safe to use concurrently with reads and writes to the Reliable Dictionary.
        /// It represents a snapshot consistent view. Example usage can be
        /// seen <see href="https://github.com/Azure-Samples/service-fabric-dotnet-web-reference-app/blob/master/ReferenceApp/Inventory.Service/InventoryService.cs">here</see>.</remarks>
        /// <returns>An enumerable for the <see cref="IReliableDictionary3{TKey, TValue}"/> versioned key/value pairs.</returns>
        Task<IAsyncEnumerable<VersionedKeyValuePair<TKey, TValue>>> CreateVersionedEnumerableAsync(ITransaction txn, Func<TKey, bool> filter, TKey firstKey, TKey lastKey);
    }
}
