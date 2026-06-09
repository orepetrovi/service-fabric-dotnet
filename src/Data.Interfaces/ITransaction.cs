// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    using System;
    using System.Fabric;
    using System.Threading.Tasks;

    using Microsoft.ServiceFabric.Data.Collections;

    /// <summary>
    /// Represents a sequence of operations performed as a single logical unit of work.
    /// </summary>
    /// <remarks>
    /// A transaction must exhibit the following <see href="https://learn.microsoft.com/windows/win32/cossdk/acid-properties">ACID properties</see>:
    /// <list type="bullet">
    ///     <item>
    ///         <term>Atomicity</term>
    ///         <description>A transaction must be an atomic unit of work; either all of its data modifications are performed, or none of them is performed.</description>
    ///     </item>
    ///     <item>
    ///         <term>Consistency</term>
    ///         <description>When completed, a transaction must leave all data in a consistent state. All internal data structures must be correct at the end of the transaction.</description>
    ///     </item>
    ///     <item>
    ///         <term>Isolation</term>
    ///         <description>Modifications made by concurrent transactions must be isolated from the modifications made by any other concurrent transactions. The isolation level used for an operation is determined by the <see cref="IReliableState"/> performing the operation.</description>
    ///     </item>
    ///     <item>
    ///         <term>Durability</term>
    ///         <description>After a transaction has completed, its effects are permanently in place in the system. The modifications persist even in the event of a system failure.</description>
    ///     </item>
    /// </list>
    /// <para>
    /// Instance members of this type are not guaranteed to be thread-safe.
    /// This makes transactions the unit of concurrency: Users can have multiple transactions in-flight at any time, but for a given transaction each API must be called one at a time.
    /// All <see cref="IReliableCollection{T}"/> APIs that accept a transaction and return a <see cref="Task"/> must be awaited one at a time.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following is an example of correct usage.
    /// <code language="csharp">
    /// <![CDATA[
    /// while (true)
    /// {
    ///     cancellationToken.ThrowIfCancellationRequested();
    /// 
    ///     try
    ///     {
    ///         using (var tx = this.StateManager.CreateTransaction())
    ///         {
    ///             await concurrentQueue.EnqueueAsync(tx, 12L, cancellationToken);
    ///             await tx.CommitAsync();
    ///
    ///             return;
    ///         }
    ///     }
    ///     catch (TransactionFaultedException e)
    ///     {
    ///         // This indicates that the transaction was internally faulted by the system. One possible cause for this
    ///         // is that the transaction was long running and blocked a checkpoint. Increasing the
    ///         // "ReliableStateManagerReplicatorSettings.CheckpointThresholdInMB" will help reduce the chances of
    ///         // running into this exception.
    ///         Console.WriteLine("Transaction was internally faulted, retrying the transaction: " + e);
    ///     }
    ///     catch (FabricNotPrimaryException e)
    ///     {
    ///         // Gracefully exit RunAsync as the new primary should have RunAsync invoked on it and continue work.
    ///         // If instead enqueue was being executed as part of a client request, the client would be signaled to re-resolve.
    ///         Console.WriteLine("Replica is not primary, exiting RunAsync: " + e);
    ///         return;
    ///     }
    ///     catch (FabricNotReadableException e)
    ///     {
    ///         // Retry until the queue is readable or a different exception is thrown.
    ///         Console.WriteLine("Queue is not readable, retrying the transaction: " + e);
    ///     }
    ///     catch (FabricObjectClosedException e)
    ///     {
    ///         // Gracefully exit RunAsync as this is happening due to replica close.
    ///         // If instead enqueue was being executed as part of a client request, the client would be signaled to re-resolve.
    ///         Console.WriteLine("Replica is closing, exiting RunAsync: " + e);
    ///         return;
    ///     }
    ///     catch (TimeoutException e)
    ///     {
    ///         Console.WriteLine("Encountered TimeoutException during EnqueueAsync, retrying the transaction: " + e);
    ///     }
    ///
    ///     // Delay and retry.
    ///     await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
    /// }
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// The following is an example of incorrect usage that has undefined behavior.
    /// <code language="csharp">
    /// <![CDATA[
    /// using (var tx = this.StateManager.CreateTransaction())
    /// {
    ///     List<Task<ConditionalValue<T>>> taskList = new List<Task<ConditionalValue<T>>>();
    ///     taskList.Add(concurrentQueue.TryDequeueAsync(tx, cancellationToken));
    ///     taskList.Add(concurrentQueue.TryDequeueAsync(tx, cancellationToken));
    ///
    ///     // Both TryDequeueAsync calls are issued on the same transaction and awaited together; per the await-serialization
    ///     // rule in <remarks> above, every IReliableCollection<T> API on a transaction must be awaited before the next is started.
    ///     await Task.WhenAll(taskList);
    ///     await tx.CommitAsync();
    /// }
    /// ]]>
    /// </code>
    /// </example>
    // todo: ITransaction inherits Dispose from IDisposable and the canonical-usage <example> above relies on a using
    // block, but Dispose semantics cannot be verified from this repository; extend the interface <remarks> with a
    // sentence describing whether disposing an uncommitted transaction implicitly aborts it, whether Dispose is
    // idempotent after a successful CommitAsync or Abort, and whether Dispose can throw or swallows faults raised
    // during implicit abort, once domain knowledge is available.
    public interface ITransaction : IDisposable
    {
        /// <summary>
        /// Gets the sequence number assigned to the transaction when it was committed.
        /// </summary>
        // todo: pre-commit behavior of CommitSequenceNumber (value returned before CommitAsync succeeds, or exception thrown)
        // cannot be verified from this repository; document this in the summary or a <remarks> block once domain knowledge is available
        long CommitSequenceNumber { get; }

        /// <summary>
        /// Commits the transaction.
        /// </summary>
        /// <remarks>
        /// A committed transaction cannot be aborted, because all modifications have been persisted and replicated.
        /// </remarks>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">The transaction has already been committed or aborted.</exception>
        /// <exception cref="FabricNotPrimaryException">
        /// The transaction includes updates to <see cref="IReliableState"/> and the replica is not in the <see cref="ReplicaRole.Primary"/> role.
        /// Only <see cref="ReplicaRole.Primary"/> replicas are given write status.
        /// </exception>
        Task CommitAsync();

        /// <summary>
        /// Aborts the transaction, rolling back any uncommitted operations.
        /// </summary>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">The transaction has already been committed or aborted.</exception>
        /// <exception cref="FabricNotPrimaryException">
        /// The transaction includes updates to <see cref="IReliableState"/> and the replica is not in the <see cref="ReplicaRole.Primary"/> role.
        /// Only <see cref="ReplicaRole.Primary"/> replicas are given write status.
        /// </exception>
        // todo: verify whether Abort actually throws TransactionFaultedException (the natural recovery from a faulted
        // transaction is to abort it, so the documented contract is suspect) and whether Abort is idempotent or instead
        // throws InvalidOperationException on a second call (ITransaction : IDisposable, so Dispose-on-exception paths
        // depend on this); the FabricNotPrimaryException claim on Abort is already tracked in
        // ITransaction.cs-needs-human-review.md and is not duplicated here
        void Abort();

        /// <summary>
        /// Gets the identifier of the transaction.
        /// </summary>
        // todo: TransactionId semantics (uniqueness scope, assignment, monotonicity, lifetime, valid range) cannot be verified
        // from this repository; rewrite the summary to describe these properties once domain knowledge is available
        long TransactionId { get; }

        /// <summary>
        /// Returns the visibility sequence number for this transaction.
        /// </summary>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">The transaction has already been committed or aborted.</exception>
        // todo: "visibility sequence number" semantics cannot be verified from this repository; rewrite the summary to
        // describe what the returned value represents and how callers should use it, and add a <returns> element only if
        // it carries information beyond the rewritten summary; verify whether FabricNotPrimaryException or other
        // role/readability exceptions apply to this member once domain knowledge is available.
        Task<long> GetVisibilitySequenceNumberAsync();
    }
}
