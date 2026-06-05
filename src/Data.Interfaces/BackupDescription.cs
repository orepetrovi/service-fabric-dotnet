// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Contains the information necessary to back up a stateful service replica.
    /// </summary>
    public struct BackupDescription
    {
        private readonly BackupOption option;
        private readonly Func<BackupInfo, CancellationToken, Task<bool>> backupCallback;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackupDescription"/> struct.
        /// </summary>
        /// <inheritdoc cref="BackupDescription(BackupOption, Func{BackupInfo, CancellationToken, Task{bool}})" path="/param[@name='backupCallback']"/>
        /// <remarks>
        /// Uses <see cref="BackupOption.Full"/> for the backup option.
        /// </remarks>
        public BackupDescription(Func<BackupInfo, CancellationToken, Task<bool>> backupCallback)
        {
            this.option = BackupOption.Full;
            this.backupCallback = backupCallback;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BackupDescription"/> struct.
        /// </summary>
        /// <param name="option">
        /// The <see cref="BackupOption"/> for the backup.
        /// </param>
        /// <param name="backupCallback">
        /// Callback to be called when the backup folder has been created locally and is ready to be moved out of the node.
        /// </param>
        public BackupDescription(BackupOption option, Func<BackupInfo, CancellationToken, Task<bool>> backupCallback)
        {
            this.option = option;
            this.backupCallback = backupCallback;
        }

        /// <summary>
        /// Gets the kind of backup to perform.
        /// </summary>
        public BackupOption Option
        {
            get
            {
                return this.option;
            }
        }

        /// <summary>
        /// Gets the callback to be called when the backup folder has been created locally and is ready to be moved out of the node.
        /// </summary>
        /// <value>
        /// The backup callback function commonly used to copy the backup folder to an external location.
        /// </value>
        /// <remarks>
        /// The <see langword="bool"/> returned by <see cref="BackupCallback"/> indicates whether the service was able to successfully move the backup folder to an external location.
        /// If <see langword="false"/> is returned,
        /// <see cref="IStateProviderReplica.BackupAsync(BackupOption, TimeSpan, CancellationToken, Func{BackupInfo, CancellationToken, Task{bool}})"/>
        /// throws <see cref="InvalidOperationException"/> indicating <see cref="BackupCallback"/> returned <see langword="false"/>,
        /// and the backup is marked as unsuccessful.
        /// </remarks>
        public Func<BackupInfo, CancellationToken, Task<bool>> BackupCallback
        {
            get
            {
                return this.backupCallback;
            }
        }
    }
}
