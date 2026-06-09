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
    /// Represents a reliable first-in, first-out collection of objects that are persisted and replicated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Values stored in this queue MUST NOT be mutated outside the context of an operation on the queue. It is
    /// highly recommended to make <typeparamref name="T"/> immutable in order to avoid accidental data corruption.
    /// </para>
    /// <para>
    /// A transaction is the unit of concurrency: Users can have multiple transactions in-flight at any time, but for a given transaction each API must be called one at a time.
    /// So all Reliable Collection APIs that take in a transaction and return a <see cref="Task"/> must be awaited one at a time.
    /// </para>
    /// </remarks>
    /// <seealso cref="ITransaction"/>
    public interface IReliableQueue<T> : IReliableCollection<T>
    {
        /// <summary>
        /// Adds an object to the end of the reliable queue.
        /// </summary>
        /// <param name="tx">Transaction to associate this operation with.</param>
        /// <param name="item">The object to add to the end of the queue. The value can be <see langword="null"/> for reference types.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is null.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableQueue{T}"/> is not in the <see cref="ReplicaRole.Primary"/> role.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// For example, the transaction used was already terminated: committed or aborted by the user.
        /// This exception typically indicates a bug in the service's use of transactions.
        /// </exception>
        /// <remarks>If a retriable exception is thrown by this method, it is recommended to dispose the transaction <paramref name="tx"/> and try again with a new transaction.</remarks>
        Task EnqueueAsync(ITransaction tx, T item);

        /// <summary>
        /// Adds an object to the end of the reliable queue.
        /// </summary>
        /// <param name="tx">Transaction to associate this operation with.</param>
        /// <param name="item">The object to add to the end of the queue. The value can be <see langword="null"/> for reference types.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks. The default is 4 seconds.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableQueue{T}"/> is not in the <see cref="ReplicaRole.Primary"/> role.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// For example, the transaction used was already terminated: committed or aborted by the user.
        /// This exception typically indicates a bug in the service's use of transactions.
        /// </exception>
        /// <remarks>If a retriable exception is thrown by this method, it is recommended to dispose the transaction <paramref name="tx"/> and try again with a new transaction.</remarks>
        Task EnqueueAsync(ITransaction tx, T item, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the value removed from the beginning of the reliable queue, or an empty result if the queue is empty.
        /// </summary>
        /// <param name="tx">Transaction to associate this operation with.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is null.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableQueue{T}"/> is not in the <see cref="ReplicaRole.Primary"/> role.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// For example, the transaction used was already terminated: committed or aborted by the user.
        /// This exception typically indicates a bug in the service's use of transactions.
        /// </exception>
        /// <remarks>If a retriable exception is thrown by this method, it is recommended to dispose the transaction <paramref name="tx"/> and try again with a new transaction.</remarks>
        /// <returns>
        /// The value removed from the beginning of the queue via <see cref="ConditionalValue{T}.Value"/> with <see cref="ConditionalValue{T}.HasValue"/> set to <see langword="true"/> when the queue was not empty; otherwise, <see cref="ConditionalValue{T}.HasValue"/> is <see langword="false"/>.
        /// </returns>
        Task<ConditionalValue<T>> TryDequeueAsync(ITransaction tx);

        /// <summary>
        /// Returns the value removed from the beginning of the reliable queue, or an empty result if the queue is empty.
        /// </summary>
        /// <param name="tx">Transaction to associate this operation with.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks. The default is 4 seconds.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableQueue{T}"/> is not in the <see cref="ReplicaRole.Primary"/> role.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// For example, the transaction used was already terminated: committed or aborted by the user.
        /// This exception typically indicates a bug in the service's use of transactions.
        /// </exception>
        /// <remarks>If a retriable exception is thrown by this method, it is recommended to dispose the transaction <paramref name="tx"/> and try again with a new transaction.</remarks>
        /// <returns>
        /// The value removed from the beginning of the queue via <see cref="ConditionalValue{T}.Value"/> with <see cref="ConditionalValue{T}.HasValue"/> set to <see langword="true"/> when the queue was not empty; otherwise, <see cref="ConditionalValue{T}.HasValue"/> is <see langword="false"/>.
        /// </returns>
        Task<ConditionalValue<T>> TryDequeueAsync(ITransaction tx, TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns the value at the beginning of the reliable queue without removing it, or an empty result if the queue is empty.
        /// </summary>
        /// <param name="tx">Transaction to associate this operation with.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is null.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableQueue{T}"/> cannot serve reads.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableQueue{T}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// For example, the transaction used was already terminated: committed or aborted by the user.
        /// This exception typically indicates a bug in the service's use of transactions.
        /// </exception>
        /// <returns>
        /// The value at the beginning of the queue via <see cref="ConditionalValue{T}.Value"/> with <see cref="ConditionalValue{T}.HasValue"/> set to <see langword="true"/> when the queue was not empty; otherwise, <see cref="ConditionalValue{T}.HasValue"/> is <see langword="false"/>.
        /// </returns>
        Task<ConditionalValue<T>> TryPeekAsync(ITransaction tx);

        /// <summary>
        /// Returns the value at the beginning of the reliable queue without removing it, or an empty result if the queue is empty.
        /// </summary>
        /// <param name="tx">Transaction to associate this operation with.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks. The default is 4 seconds.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableQueue{T}"/> cannot serve reads.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableQueue{T}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// For example, the transaction used was already terminated: committed or aborted by the user.
        /// This exception typically indicates a bug in the service's use of transactions.
        /// </exception>
        /// <returns>
        /// The value at the beginning of the queue via <see cref="ConditionalValue{T}.Value"/> with <see cref="ConditionalValue{T}.HasValue"/> set to <see langword="true"/> when the queue was not empty; otherwise, <see cref="ConditionalValue{T}.HasValue"/> is <see langword="false"/>.
        /// </returns>
        Task<ConditionalValue<T>> TryPeekAsync(ITransaction tx, TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns the value at the beginning of the reliable queue without removing it, or an empty result if the queue is empty.
        /// </summary>
        /// <param name="tx">Transaction to associate this operation with.</param>
        /// <param name="lockMode">Type of locking to use for this read operation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is null.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the default timeout.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableQueue{T}"/> cannot serve reads.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableQueue{T}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// For example, the transaction used was already terminated: committed or aborted by the user.
        /// This exception typically indicates a bug in the service's use of transactions.
        /// </exception>
        /// <returns>
        /// The value at the beginning of the queue via <see cref="ConditionalValue{T}.Value"/> with <see cref="ConditionalValue{T}.HasValue"/> set to <see langword="true"/> when the queue was not empty; otherwise, <see cref="ConditionalValue{T}.HasValue"/> is <see langword="false"/>.
        /// </returns>
        Task<ConditionalValue<T>> TryPeekAsync(ITransaction tx, LockMode lockMode);

        /// <summary>
        /// Returns the value at the beginning of the reliable queue without removing it, or an empty result if the queue is empty.
        /// </summary>
        /// <param name="tx">Transaction to associate this operation with.</param>
        /// <param name="lockMode">Type of locking to use for this read operation.</param>
        /// <param name="timeout">The amount of time to wait for the operation to complete before throwing a <see cref="TimeoutException"/>. Primarily used to prevent deadlocks. The default is 4 seconds.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the given timeout.</exception>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableQueue{T}"/> cannot serve reads.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableQueue{T}"/> is not yet consistent.
        /// </exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">
        /// A method call is invalid for the object's current state.
        /// For example, the transaction used was already terminated: committed or aborted by the user.
        /// This exception typically indicates a bug in the service's use of transactions.
        /// </exception>
        /// <returns>
        /// The value at the beginning of the queue via <see cref="ConditionalValue{T}.Value"/> with <see cref="ConditionalValue{T}.HasValue"/> set to <see langword="true"/> when the queue was not empty; otherwise, <see cref="ConditionalValue{T}.HasValue"/> is <see langword="false"/>.
        /// </returns>
        Task<ConditionalValue<T>> TryPeekAsync(ITransaction tx, LockMode lockMode, TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns an async enumerable over the <see cref="IReliableQueue{T}"/>.
        /// </summary>
        /// <param name="tx">Transaction to associate this operation with.</param>
        /// <exception cref="FabricNotReadableException">
        /// The <see cref="IReliableQueue{T}"/> cannot serve reads.
        /// This exception can be thrown in all <see cref="ReplicaRole"/>s.
        /// One reason it may be thrown in the <see cref="ReplicaRole.Primary"/> role is loss of <see cref="IStatefulServicePartition.ReadStatus"/>.
        /// One reason it may be thrown in the <see cref="ReplicaRole.ActiveSecondary"/> role is that the state of the <see cref="IReliableQueue{T}"/> is not yet consistent.
        /// </exception>
        /// <returns>A task whose result is an <see cref="IAsyncEnumerable{T}"/> over all values in the queue.</returns>
        Task<IAsyncEnumerable<T>> CreateEnumerableAsync(ITransaction tx);
    }
}
