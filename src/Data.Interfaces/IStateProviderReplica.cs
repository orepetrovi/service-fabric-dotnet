// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    using System;
    using System.Fabric;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the members a reliable state provider replica must implement for Service Fabric to interact with it.
    /// </summary>
    public interface IStateProviderReplica
    {
        /// <summary>
        /// Sets the callback invoked during suspected data loss.
        /// </summary>
        /// <value>
        /// The callback that represents the asynchronous processing of the data-loss event. Returning
        /// <see langword="true"/> indicates the replica's state has been restored; <see langword="false"/> indicates it has
        /// not been changed.
        /// </value>
        /// <remarks>
        /// This callback is where <see cref="RestoreAsync(string)"/> may be invoked. It runs while the replica does not have
        /// read or write status, so reads and writes against the state providers are not permitted. Returning
        /// <see langword="true"/> causes the Primary to rebuild the other replicas in the partition from the restored state.
        /// </remarks>
        Func<CancellationToken, Task<bool>> OnDataLossAsync { set; }

        /// <summary>
        /// Initializes the state provider replica using the service initialization information.
        /// </summary>
        /// <remarks>
        /// No complex processing should be done during <see cref="Initialize(StatefulServiceInitializationParameters)"/>. Expensive or
        /// long-running initialization should be done in <see cref="OpenAsync(ReplicaOpenMode, IStatefulServicePartition, CancellationToken)"/>.
        /// </remarks>
        /// <param name="initializationParameters">The service initialization information such as service name, partition id, replica id, and code package information.</param>
        void Initialize(StatefulServiceInitializationParameters initializationParameters);

        /// <summary>
        /// Asynchronously opens the state provider replica for use.
        /// </summary>
        /// <remarks>
        /// Extended state provider initialization tasks can be started at this time.
        /// </remarks>
        /// <param name="openMode">One of the enumeration values that specifies whether this is a new or existing replica.</param>
        /// <param name="partition">The partition this replica belongs to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// The replicator responsible for replicating state between other state provider replicas in the partition.
        /// </returns>
        Task<IReplicator> OpenAsync(ReplicaOpenMode openMode, IStatefulServicePartition partition,
            CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously notifies the state provider replica that its role is changing, for example to Primary or Secondary.
        /// </summary>
        /// <param name="newRole">One of the enumeration values that specifies the new replica role, such as primary or secondary.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        Task ChangeRoleAsync(ReplicaRole newRole, CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously closes the state provider replica gracefully.
        /// </summary>
        /// <remarks>
        /// This generally occurs when the replica's code is being upgraded, the replica is being moved
        /// due to load balancing, or a transient fault is detected.
        /// </remarks>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        Task CloseAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Aborts the state provider replica forcefully.
        /// </summary>
        /// <remarks>
        /// This generally occurs when a permanent fault is detected on the node, or when
        /// Service Fabric cannot reliably manage the replica's life-cycle due to internal failures.
        /// </remarks>
        void Abort();

        /// <summary>
        /// Asynchronously performs a full backup of all reliable state managed by this replica.
        /// </summary>
        /// <param name="backupCallback">The callback invoked when the backup folder has been created locally and is ready to be moved out of the node.</param>
        /// <remarks>
        /// A full backup will be performed with no timeout. To specify a timeout, use the
        /// <see cref="BackupAsync(BackupOption, TimeSpan, CancellationToken, Func{BackupInfo, CancellationToken, Task{bool}})"/> overload.
        /// The Boolean returned by <paramref name="backupCallback"/> indicates whether the service successfully moved the backup folder to an external location.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="backupCallback"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="backupCallback"/> returned <see langword="false"/>; the backup is marked unsuccessful.</exception>
        /// <exception cref="FabricBackupInProgressException">Another backup is already in progress.</exception>
        Task BackupAsync(Func<BackupInfo, CancellationToken, Task<bool>> backupCallback);

        /// <summary>
        /// Asynchronously performs a backup of all reliable state managed by this replica.
        /// </summary>
        /// <param name="option">One of the enumeration values that specifies the type of backup to perform.</param>
        /// <param name="timeout">The timeout for this operation.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <param name="backupCallback">The callback invoked when the backup folder has been created locally and is ready to be moved out of the node.</param>
        /// <remarks>
        /// The Boolean returned by <paramref name="backupCallback"/> indicates whether the service successfully moved the backup folder to an external location.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="backupCallback"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative (other than <see cref="Timeout.InfiniteTimeSpan"/>) or greater than <see cref="Int32.MaxValue"/> milliseconds.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="backupCallback"/> returned <see langword="false"/>; the backup is marked unsuccessful.</exception>
        /// <exception cref="TimeoutException">The backup did not complete within <paramref name="timeout"/>.</exception>
        /// <exception cref="FabricBackupInProgressException">Another backup is already in progress.</exception>
        Task BackupAsync(
            BackupOption option,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            Func<BackupInfo, CancellationToken, Task<bool>> backupCallback);

        /// <summary>
        /// Asynchronously restores a backup taken by <see cref="BackupAsync(Func{BackupInfo, CancellationToken, Task{bool}})"/> or 
        /// <see cref="BackupAsync(BackupOption, TimeSpan, CancellationToken, Func{BackupInfo, CancellationToken, Task{bool}})"/>.
        /// </summary>
        /// <param name="backupFolderPath">
        /// The directory where the replica is to be restored from.
        /// This parameter cannot be null, empty or contain just whitespace. 
        /// UNC paths may also be provided.
        /// </param>
        /// <remarks>
        /// A safe restore will be performed, meaning the restore will only be completed if the data to restore is ahead of state of the current replica.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="backupFolderPath"/> is <see langword="null"/>, empty, whitespace, or is not a valid backup folder.</exception>
        /// <exception cref="InvalidOperationException">The method is invoked outside of <see cref="OnDataLossAsync"/> processing.</exception>
        Task RestoreAsync(string backupFolderPath);

        /// <summary>
        /// Asynchronously restores a backup taken by <see cref="BackupAsync(Func{BackupInfo, CancellationToken, Task{bool}})"/> or 
        /// <see cref="BackupAsync(BackupOption, TimeSpan, CancellationToken, Func{BackupInfo, CancellationToken, Task{bool}})"/>.
        /// </summary>
        /// <param name="backupFolderPath">
        /// The directory where the replica is to be restored from.
        /// This parameter cannot be null, empty or contain just whitespace. 
        /// UNC paths may also be provided.
        /// </param>
        /// <param name="restorePolicy">One of the enumeration values that specifies the policy applied when restoring from backup.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentException"><paramref name="backupFolderPath"/> is <see langword="null"/>, empty, whitespace, or is not a valid backup folder.</exception>
        /// <exception cref="InvalidOperationException">The method is invoked outside of <see cref="OnDataLossAsync"/> processing.</exception>
        Task RestoreAsync(
            string backupFolderPath,
            RestorePolicy restorePolicy,
            CancellationToken cancellationToken);
    }
}
