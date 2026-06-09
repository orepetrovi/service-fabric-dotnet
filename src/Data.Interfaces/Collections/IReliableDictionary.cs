// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.ServiceFabric.Data.Notifications;

    /// <summary>
    /// Represents a Reliable Collection of key/value pairs that are persisted and replicated.
    /// </summary>
    /// <remarks>
    /// <para>Keys or values stored in this dictionary MUST NOT be mutated outside the context of an operation on the 
    /// dictionary. It is highly recommended to make both <typeparamref name="TKey"/> and <typeparamref name="TValue"/> 
    /// immutable to avoid accidental data corruption.
    /// See <see href="https://docs.microsoft.com/azure/service-fabric/service-fabric-work-with-reliable-collections">here</see> for common pitfalls.</para>
    /// <para>The transaction is the unit of concurrency. Users can have multiple transactions in flight at any time, but for a given transaction each API must be called one at a time.
    /// When calling any asynchronous Reliable Collection method that takes an <see cref="ITransaction"/>, you must wait for completion of the returned Task before calling
    /// another method using the same transaction. See examples of transactions <see href="https://docs.microsoft.com/azure/service-fabric/service-fabric-work-with-reliable-collections">here</see>.</para>
    /// </remarks>
    // todo: the <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception> text on
    // the 14 overloads that take a TimeSpan timeout may over-promise rejection: Timeout.InfiniteTimeSpan has
    // Ticks = -1 (strictly negative) and is a common wait-forever sentinel across .NET and Service Fabric APIs.
    // Verify whether this interface accepts Timeout.InfiniteTimeSpan; if so, refine each <exception
    // cref="ArgumentException"> description to exclude that sentinel (e.g. "is negative and not <see
    // cref="Timeout.InfiniteTimeSpan"/>"). Note: AddAsync's <exception cref="ArgumentException"> body combines
    // "A value with the same key already exists in the Reliable Dictionary, or <paramref name="timeout"/> is
    // negative." - apply the same refinement to the timeout clause there.
    public interface IReliableDictionary<TKey, TValue> : IReliableCollection<KeyValuePair<TKey, TValue>>
        where TKey : IComparable<TKey>, IEquatable<TKey>
    {
        /// <summary>
        /// Sets the function that is called when the Reliable Dictionary is being rebuilt during copy, restore or recovery.
        /// </summary>
        /// <remarks>
        /// <see cref="NotifyDictionaryChangedEventArgs{TKey, TValue}"/> can only be used within this callback.
        /// Once the asynchronous callback completes, the <see cref="NotifyDictionaryRebuildEventArgs{TKey, TValue}"/> becomes invalid. 
        /// See <see href="https://docs.microsoft.com/azure/service-fabric/service-fabric-reliable-services-notifications">here</see> for more information. 
        /// </remarks>
        /// <value>The asynchronous rebuild notification callback.</value>
        // todo: <remarks> first sentence references <see cref="NotifyDictionaryChangedEventArgs{TKey, TValue}"/>, but this
        // property's setter receives <see cref="NotifyDictionaryRebuildEventArgs{TKey, TValue}"/> per the Func
        // signature below; NotifyDictionaryChangedEventArgs is the args type for the separate DictionaryChanged event.
        // The intended sentence likely reads "NotifyDictionaryRebuildEventArgs ... can only be used within this
        // callback." Verify the lifetime contract (both "can only be used within this callback" and "becomes invalid
        // after the callback completes") against the runtime implementation before fixing the <remarks> inline.
        Func<IReliableDictionary<TKey, TValue>, NotifyDictionaryRebuildEventArgs<TKey, TValue>, Task> RebuildNotificationAsyncCallback
        {
            set;
        }

        /// <summary>
        /// Occurs when the Reliable Dictionary changes, for example, an item is added, updated, or removed.
        /// </summary>
        event EventHandler<NotifyDictionaryChangedEventArgs<TKey, TValue>> DictionaryChanged;

        /// <summary>
        /// Adds the specified key/value pair to the Reliable Dictionary.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key to be added.</param>
        /// <param name="value">The value to be added. The value can be <see langword="null"/> for reference types.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException">A value with the same key already exists in the Reliable Dictionary.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        Task AddAsync(ITransaction tx, TKey key, TValue value);

        /// <summary>
        /// Adds the specified key/value pair to the Reliable Dictionary.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key to be added.</param>
        /// <param name="value">The value to be added. The value can be <see langword="null"/> for reference types.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException">A value with the same key already exists in the Reliable Dictionary, or <paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        Task AddAsync(ITransaction tx, TKey key, TValue value, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the value associated with the specified key in the Reliable Dictionary after adding it using <paramref name="addValueFactory"/> if absent or updating it using <paramref name="updateValueFactory"/> if present.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key to be added or whose value should be updated.</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key.</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized, or <paramref name="addValueFactory"/> is <see langword="null"/>, or <paramref name="updateValueFactory"/> is <see langword="null"/>.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>The new value for the key: the result of <paramref name="addValueFactory"/> if the key was absent, or the result of <paramref name="updateValueFactory"/> if the key was present.</returns>
        Task<TValue> AddOrUpdateAsync(
            ITransaction tx, 
            TKey key, 
            Func<TKey, TValue> addValueFactory,
            Func<TKey, TValue, TValue> updateValueFactory);

        /// <summary>
        /// Returns the value associated with the specified key in the Reliable Dictionary after adding <paramref name="addValue"/> if absent or updating it using <paramref name="updateValueFactory"/> if present.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key to be added or whose value should be updated.</param>
        /// <param name="addValue">The value to be added for an absent key. The value can be <see langword="null"/> for reference types.</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized, or <paramref name="updateValueFactory"/> is <see langword="null"/>.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>The new value for the key: <paramref name="addValue"/> if the key was absent, or the result of <paramref name="updateValueFactory"/> if the key was present.</returns>
        Task<TValue> AddOrUpdateAsync(
            ITransaction tx, 
            TKey key, 
            TValue addValue,
            Func<TKey, TValue, TValue> updateValueFactory);

        /// <summary>
        /// Returns the value associated with the specified key in the Reliable Dictionary after adding it using <paramref name="addValueFactory"/> if absent or updating it using <paramref name="updateValueFactory"/> if present.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key to be added or whose value should be updated.</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key.</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized, or <paramref name="addValueFactory"/> is <see langword="null"/>, or <paramref name="updateValueFactory"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>The new value for the key: the result of <paramref name="addValueFactory"/> if the key was absent, or the result of <paramref name="updateValueFactory"/> if the key was present.</returns>
        Task<TValue> AddOrUpdateAsync(
            ITransaction tx, 
            TKey key, 
            Func<TKey, TValue> addValueFactory,
            Func<TKey, TValue, TValue> updateValueFactory, 
            TimeSpan timeout, 
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns the value associated with the specified key in the Reliable Dictionary after adding <paramref name="addValue"/> if absent or updating it using <paramref name="updateValueFactory"/> if present.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key to be added or whose value should be updated.</param>
        /// <param name="addValue">The value to be added for an absent key. The value can be <see langword="null"/> for reference types.</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on the key's existing value.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized, or <paramref name="updateValueFactory"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>The new value for the key: <paramref name="addValue"/> if the key was absent, or the result of <paramref name="updateValueFactory"/> if the key was present.</returns>
        Task<TValue> AddOrUpdateAsync(
            ITransaction tx, 
            TKey key, 
            TValue addValue,
            Func<TKey, TValue, TValue> updateValueFactory, 
            TimeSpan timeout, 
            CancellationToken cancellationToken);

        /// <summary>
        /// Removes all keys and values from the Reliable Dictionary.
        /// </summary>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        Task ClearAsync(TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Returns a value indicating whether the Reliable Dictionary contains the specified key.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key to locate in the Reliable Dictionary.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableDictionary{TKey, TValue}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns><see langword="true"/> if the Reliable Dictionary contains the key; otherwise, <see langword="false"/>.</returns>
        Task<bool> ContainsKeyAsync(ITransaction tx, TKey key);

        /// <summary>
        /// Returns a value indicating whether the Reliable Dictionary contains the specified key.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key to locate in the Reliable Dictionary.</param>
        /// <param name="lockMode">One of the enumeration values that specifies the type of locking to use for this read operation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableDictionary{TKey, TValue}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns><see langword="true"/> if the Reliable Dictionary contains the key; otherwise, <see langword="false"/>.</returns>
        Task<bool> ContainsKeyAsync(ITransaction tx, TKey key, LockMode lockMode);

        /// <summary>
        /// Returns a value indicating whether the Reliable Dictionary contains the specified key.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key to locate in the Reliable Dictionary.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableDictionary{TKey, TValue}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns><see langword="true"/> if the Reliable Dictionary contains the key; otherwise, <see langword="false"/>.</returns>
        Task<bool> ContainsKeyAsync(ITransaction tx, TKey key, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Returns a value indicating whether the Reliable Dictionary contains the specified key.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key to locate in the Reliable Dictionary.</param>
        /// <param name="lockMode">One of the enumeration values that specifies the type of locking to use for this read operation.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableDictionary{TKey, TValue}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns><see langword="true"/> if the Reliable Dictionary contains the key; otherwise, <see langword="false"/>.</returns>
        Task<bool> ContainsKeyAsync(
            ITransaction tx, 
            TKey key, 
            LockMode lockMode, 
            TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns an <see cref="IAsyncEnumerable{T}"/> over the <see cref="IReliableDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableDictionary{TKey, TValue}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <remarks>
        /// The returned enumerable is safe to use concurrently with reads and writes to the Reliable Dictionary.
        /// It represents a snapshot consistent view. Please note that <see cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/> needs to be called on
        /// the returned instance to enumerate. Example usage can be
        /// seen <see href="https://github.com/Azure-Samples/service-fabric-dotnet-web-reference-app/blob/master/ReferenceApp/Inventory.Service/InventoryService.cs">here</see>.
        /// </remarks>
        /// <returns>A task whose result is an <see cref="IAsyncEnumerable{T}"/> over the key/value pairs in the <see cref="IReliableDictionary{TKey,TValue}"/>.</returns>
        Task<Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<TKey, TValue>>> CreateEnumerableAsync(ITransaction txn);

        /// <summary>
        /// Returns an <see cref="IAsyncEnumerable{T}"/> over the <see cref="IReliableDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <param name="enumerationMode">One of the enumeration values that specifies the enumeration mode to use.</param>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableDictionary{TKey, TValue}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <remarks>
        /// The returned enumerable is safe to use concurrently with reads and writes to the Reliable Dictionary.
        /// It represents a snapshot consistent view. Please note that <see cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/> needs to be called on
        /// the returned instance to enumerate. Example usage can be
        /// seen <see href="https://github.com/Azure-Samples/service-fabric-dotnet-web-reference-app/blob/master/ReferenceApp/Inventory.Service/InventoryService.cs">here</see>.
        /// </remarks>
        /// <returns>A task whose result is an <see cref="IAsyncEnumerable{T}"/> over the key/value pairs in the <see cref="IReliableDictionary{TKey,TValue}"/>.</returns>
        Task<Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<TKey, TValue>>> CreateEnumerableAsync(ITransaction txn, EnumerationMode enumerationMode);

        /// <summary>
        /// Returns an <see cref="IAsyncEnumerable{T}"/> over the <see cref="IReliableDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <param name="txn">The transaction to associate this operation with.</param>
        /// <param name="filter">A predicate that filters the key-value pairs to include in the enumeration based on the key.</param>
        /// <param name="enumerationMode">One of the enumeration values that specifies the enumeration mode to use.</param>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableDictionary{TKey, TValue}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <remarks>
        /// The returned enumerable is safe to use concurrently with reads and writes to the Reliable Dictionary.
        /// It represents a snapshot consistent view. Please note that <see cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/> needs to be called on
        /// the returned instance to enumerate. Example usage can be
        /// seen <see href="https://github.com/Azure-Samples/service-fabric-dotnet-web-reference-app/blob/master/ReferenceApp/Inventory.Service/InventoryService.cs">here</see>.
        /// </remarks>
        /// <returns>A task whose result is an <see cref="IAsyncEnumerable{T}"/> over the key/value pairs in the <see cref="IReliableDictionary{TKey,TValue}"/>.</returns>
        Task<Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<TKey, TValue>>> CreateEnumerableAsync(
            ITransaction txn, 
            Func<TKey, bool> filter,
            EnumerationMode enumerationMode);

        /// <summary>
        /// Returns the value associated with the specified key in the Reliable Dictionary, adding a value produced by <paramref name="valueFactory"/> if the key is not present.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The function used to generate a value for the key.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized, or <paramref name="valueFactory"/> is <see langword="null"/>.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>The value associated with the key: the existing value if the key was already present, or the value returned by <paramref name="valueFactory"/> otherwise.</returns>
        Task<TValue> GetOrAddAsync(ITransaction tx, TKey key, Func<TKey, TValue> valueFactory);

        /// <summary>
        /// Returns the value associated with the specified key in the Reliable Dictionary, adding <paramref name="value"/> if the key is not present.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value to be added, if the key does not already exist.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>The value associated with the key: the existing value if the key was already present, or <paramref name="value"/> otherwise.</returns>
        Task<TValue> GetOrAddAsync(ITransaction tx, TKey key, TValue value);

        /// <summary>
        /// Returns the value associated with the specified key in the Reliable Dictionary, adding a value produced by <paramref name="valueFactory"/> if the key is not present.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The function used to generate a value for the key.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized, or <paramref name="valueFactory"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>The value associated with the key: the existing value if the key was already present, or the value returned by <paramref name="valueFactory"/> otherwise.</returns>
        Task<TValue> GetOrAddAsync(
            ITransaction tx, 
            TKey key, 
            Func<TKey, TValue> valueFactory, 
            TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns the value associated with the specified key in the Reliable Dictionary, adding <paramref name="value"/> if the key is not present.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value to be added, if the key does not already exist.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>The value associated with the key: the existing value if the key was already present, or <paramref name="value"/> otherwise.</returns>
        Task<TValue> GetOrAddAsync(
            ITransaction tx, 
            TKey key, 
            TValue value, 
            TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns a value indicating whether the specified key and value were added to the Reliable Dictionary.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add. The value can be <see langword="null"/> for reference types.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns><see langword="true"/> if the key/value pair was added; otherwise, <see langword="false"/>.</returns>
        Task<bool> TryAddAsync(ITransaction tx, TKey key, TValue value);

        /// <summary>
        /// Returns a value indicating whether the specified key and value were added to the Reliable Dictionary.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add. The value can be <see langword="null"/> for reference types.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns><see langword="true"/> if the key/value pair was added; otherwise, <see langword="false"/>.</returns>
        Task<bool> TryAddAsync(
            ITransaction tx, 
            TKey key, 
            TValue value, 
            TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns the value associated with the specified key from the Reliable Dictionary, or an empty result if the key was not found.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableDictionary{TKey, TValue}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>A result indicating whether the key was found in the Reliable Dictionary and, if so, the associated value.</returns>
        Task<ConditionalValue<TValue>> TryGetValueAsync(ITransaction tx, TKey key);

        /// <summary>
        /// Returns the value associated with the specified key from the Reliable Dictionary, or an empty result if the key was not found.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="lockMode">One of the enumeration values that specifies the type of locking to use for this read operation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableDictionary{TKey, TValue}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>A result indicating whether the key was found in the Reliable Dictionary and, if so, the associated value.</returns>
        Task<ConditionalValue<TValue>> TryGetValueAsync(ITransaction tx, TKey key, LockMode lockMode);

        /// <summary>
        /// Returns the value associated with the specified key from the Reliable Dictionary, or an empty result if the key was not found.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableDictionary{TKey, TValue}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>A result indicating whether the key was found in the Reliable Dictionary and, if so, the associated value.</returns>
        Task<ConditionalValue<TValue>> TryGetValueAsync(
            ITransaction tx, 
            TKey key, 
            TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns the value associated with the specified key from the Reliable Dictionary, or an empty result if the key was not found.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="lockMode">One of the enumeration values that specifies the type of locking to use for this read operation.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableDictionary{TKey, TValue}"/> cannot serve reads at the moment.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableDictionary{TKey, TValue}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>A result indicating whether the key was found in the Reliable Dictionary and, if so, the associated value.</returns>
        Task<ConditionalValue<TValue>> TryGetValueAsync(
            ITransaction tx, 
            TKey key, 
            LockMode lockMode,
            TimeSpan timeout, 
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns the value removed from the Reliable Dictionary, or an empty result if the specified key was not found.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to remove.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>A result indicating whether the key was removed from the Reliable Dictionary and, if so, the previous value.</returns>
        Task<ConditionalValue<TValue>> TryRemoveAsync(ITransaction tx, TKey key);

        /// <summary>
        /// Returns the value removed from the Reliable Dictionary, or an empty result if the specified key was not found.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns>A result indicating whether the key was removed from the Reliable Dictionary and, if so, the previous value.</returns>
        Task<ConditionalValue<TValue>> TryRemoveAsync(ITransaction tx, TKey key, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Returns a value indicating whether the value for the specified key was atomically updated from <paramref name="comparisonValue"/> to <paramref name="newValue"/>.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key whose value is compared with <paramref name="comparisonValue"/> and possibly replaced.</param>
        /// <param name="newValue">The value that replaces the value of the element that has the specified <paramref name="key"/> if the comparison results in equality.</param>
        /// <param name="comparisonValue">The value that is compared to the value of the element that has the specified <paramref name="key"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns><see langword="true"/> if the value was updated; otherwise, <see langword="false"/>.</returns>
        Task<bool> TryUpdateAsync(ITransaction tx, TKey key, TValue newValue, TValue comparisonValue);

        /// <summary>
        /// Returns a value indicating whether the value for the specified key was atomically updated from <paramref name="comparisonValue"/> to <paramref name="newValue"/>.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key whose value is compared with <paramref name="comparisonValue"/> and possibly replaced.</param>
        /// <param name="newValue">The value that replaces the value of the element that has the specified <paramref name="key"/> if the comparison results in equality.</param>
        /// <param name="comparisonValue">The value that is compared to the value of the element that has the specified <paramref name="key"/>.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        /// <returns><see langword="true"/> if the value was updated; otherwise, <see langword="false"/>.</returns>
        Task<bool> TryUpdateAsync(
            ITransaction tx, 
            TKey key, 
            TValue newValue, 
            TValue comparisonValue, 
            TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// Adds a key/value pair to the Reliable Dictionary if the key does not already exist, or updates a key/value pair
        /// in the Reliable Dictionary if the key already exists.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key whose value should be updated.</param>
        /// <param name="value">The value that replaces the value of the element that has the specified <paramref name="key"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        Task SetAsync(ITransaction tx, TKey key, TValue value);

        /// <summary>
        /// Adds a key/value pair to the Reliable Dictionary if the key does not already exist, or updates a key/value pair
        /// in the Reliable Dictionary if the key already exists.
        /// </summary>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key whose value should be updated.</param>
        /// <param name="value">The value that replaces the value of the element that has the specified <paramref name="key"/>.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/> or cannot be serialized.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled via <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary{TKey, TValue}"/> is closed or deleted.</exception>
        Task SetAsync(ITransaction tx, TKey key, TValue value, TimeSpan timeout, CancellationToken cancellationToken);
    }
}
