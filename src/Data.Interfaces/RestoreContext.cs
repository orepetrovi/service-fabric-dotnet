// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    using System;
    using System.Fabric;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides the ability to restore a replica's state from a backup.
    /// </summary>
    public struct RestoreContext
    {
        private readonly IStateProviderReplica stateProviderReplica;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestoreContext"/> struct.
        /// </summary>
        // todo: stateProviderReplica is not validated; passing null (or using default(RestoreContext)) causes RestoreAsync to throw NullReferenceException instead of ArgumentNullException/InvalidOperationException
        public RestoreContext(IStateProviderReplica stateProviderReplica)
        {
            this.stateProviderReplica = stateProviderReplica;
        }

        /// <inheritdoc cref="RestoreAsync(RestoreDescription, CancellationToken)"/>
        /// <remarks>
        /// This overload is equivalent to calling <see cref="RestoreAsync(RestoreDescription, CancellationToken)"/>
        /// with <see cref="CancellationToken.None"/>.
        /// </remarks>
        // todo: throws NullReferenceException when invoked on default(RestoreContext) or after passing null to the constructor; see TODO on the constructor
        public Task RestoreAsync(RestoreDescription restoreDescription)
        {
            return this.stateProviderReplica.RestoreAsync(
                restoreDescription.BackupFolderPath, 
                restoreDescription.Policy, 
                CancellationToken.None);
        }

        /// <summary>
        /// Restores a backup described by <see cref="RestoreDescription"/>.
        /// </summary>
        /// <exception cref="FabricMissingFullBackupException">
        /// The input backup folder does not contain a full backup.
        /// For a backup folder to be restorable, it must contain exactly one full backup and any number of incremental backups.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <para>
        /// One of the arguments is not valid. For example, when restoring a Reliable Service if <see cref="RestoreDescription.Policy"/> is set to <see cref="RestorePolicy.Safe"/>, 
        /// but the input backup folder contains a version of the state that is older than the state maintained in the current replica.
        /// </para>
        /// <para>
        /// When restoring an Actor Service, the specified <see cref="RestoreDescription.BackupFolderPath"/> is empty.
        /// </para>
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        /// The supplied restore directory does not exist.
        /// </exception>
        /// <exception cref="FabricObjectClosedException">
        /// The replica is closing.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current restore operation is not valid. For example, the <see cref="ServicePartitionKind"/> 
        /// of the partition from where backup was taken is different than that of current partition being restored.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// The expected backup files under the supplied restore directory are not found.
        /// </exception>
        /// <exception cref="FabricException">
        /// Either the restore operation encountered an unexpected error or the backup files in the restore directory are not valid.
        /// The <see cref="FabricException.ErrorCode"/> property indicates the type of error that occurred.
        /// <list type="bullet">
        ///     <item>
        ///         <term><see cref="FabricErrorCode.InvalidBackup"/></term>
        ///         <description>
        ///         The backup files supplied in the restore directory are either missing or contain extra unexpected files.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="FabricErrorCode.InvalidRestoreData"/></term>
        ///         <description>
        ///         The metadata files (restore.dat) present in the restore directory are either corrupt or contain invalid information.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="FabricErrorCode.InvalidBackupChain"/></term>
        ///         <description>
        ///         The backup chain (i.e. one full backup and zero or more contiguous incremental backups that were taken after it) 
        ///         supplied in the restore directory is broken. 
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="FabricErrorCode.DuplicateBackups"/></term>
        ///         <description>
        ///         The backup chain (i.e. one full backup and zero or more contiguous incremental backups that were taken after it) 
        ///         supplied in the restore directory contains duplicate backups. 
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="FabricErrorCode.RestoreSafeCheckFailed"/></term>
        ///         <description>
        ///         <see cref="RestorePolicy.Safe"/> is specified as part of <see cref="RestoreDescription"/> and
        ///         the backup provided for restore has older data than currently present in the service.
        ///         </description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <remarks>
        /// <para>
        /// This API must be called from the callback assigned to <see cref="IStateProviderReplica.OnDataLossAsync"/>. Only one call to <see cref="RestoreAsync(RestoreDescription, CancellationToken)"/> can be inflight per replica at any given point of time.
        /// </para>
        /// <para>
        /// Note that exceptions thrown by this API differ depending on the underlying state provider. The exceptions that are currently documented for
        /// this API applies only to out-of-box state providers provided by Service Fabric for Reliable Services and Reliable Actors.
        /// </para>
        /// <para>
        /// Following exceptions are thrown by this API when invoked in Reliable Service:
        /// <list type="bullet">
        ///     <item>
        ///         <description><see cref="FabricMissingFullBackupException"/></description>
        ///     </item>
        ///     <item>
        ///         <description><see cref="ArgumentException"/></description>
        ///     </item>
        /// </list>
        /// </para>
        /// <para>
        /// Following exceptions are thrown by this API when invoked in Actor Service with <c>KvsActorStateProvider</c> as its state provider (which is the
        /// default state provider for Reliable Actors):
        /// <list type="bullet">
        ///     <item>
        ///         <description><see cref="ArgumentException"/></description>
        ///     </item>
        ///     <item>
        ///         <description><see cref="DirectoryNotFoundException"/></description>
        ///     </item>
        ///     <item>
        ///         <description><see cref="FabricObjectClosedException"/></description>
        ///     </item>
        ///     <item>
        ///         <description><see cref="InvalidOperationException"/></description>
        ///     </item>
        ///     <item>
        ///         <description><see cref="FileNotFoundException"/></description>
        ///     </item>
        ///     <item>
        ///         <description><see cref="FabricException"/></description>
        ///     </item>
        /// </list>
        /// </para>
        /// </remarks>
        // todo: throws NullReferenceException when invoked on default(RestoreContext) or after passing null to the constructor; see TODO on the constructor
        public Task RestoreAsync(RestoreDescription restoreDescription, CancellationToken cancellationToken)
        {
            return this.stateProviderReplica.RestoreAsync(
                restoreDescription.BackupFolderPath, 
                restoreDescription.Policy, 
                cancellationToken);
        }
    }
}
