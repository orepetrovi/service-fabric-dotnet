// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    /// <summary>
    /// Contains the information needed to restore a stateful service replica from a backup.
    /// </summary>
    public struct RestoreDescription
    {
        private readonly string backupFolderPath;
        private readonly RestorePolicy restorePolicy;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestoreDescription"/> struct.
        /// </summary>
        /// <inheritdoc cref="RestoreDescription(string, RestorePolicy)" path="/param[@name='backupFolderPath']"/>
        /// <remarks>The restore <see cref="Policy"/> defaults to <see cref="RestorePolicy.Safe"/>.</remarks>
        public RestoreDescription(string backupFolderPath)
        {
            this.backupFolderPath = backupFolderPath;
            this.restorePolicy = RestorePolicy.Safe;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RestoreDescription"/> struct.
        /// </summary>
        /// <param name="backupFolderPath">The directory to restore the replica from.</param>
        /// <param name="restorePolicy">One of the enumeration values that specifies the policy used to restore the replica.</param>
        public RestoreDescription(string backupFolderPath, RestorePolicy restorePolicy)
        {
            this.backupFolderPath = backupFolderPath;
            this.restorePolicy = restorePolicy;
        }

        /// <summary>
        /// Gets the directory used to restore the replica's state.
        /// </summary>
        /// <remarks>
        /// The folder must contain at least one full backup and may include one or more incremental backups.
        /// UNC paths are supported.
        /// </remarks>
        // TODO: Constructors do not validate backupFolderPath. The "non-null, non-empty, non-whitespace"
        // contract is enforced only later by the restore pipeline. Consider validating here so the
        // invariant matches the documentation, or document the deferred validation explicitly.
        public string BackupFolderPath
        {
            get
            {
                return this.backupFolderPath;
            }
        }

        /// <summary>
        /// Gets the policy used to restore the replica.
        /// </summary>
        /// <value>The default is <see cref="RestorePolicy.Safe"/>.</value>
        public RestorePolicy Policy
        {
            get
            {
                return this.restorePolicy;
            }
        }
    }
}
