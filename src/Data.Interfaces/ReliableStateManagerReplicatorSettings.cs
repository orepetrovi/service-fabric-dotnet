// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    using System;
    using System.Fabric;
    using System.Text;

    /// <summary>
    /// Configures the replicator used by the reliable state manager.
    /// </summary>
    public class ReliableStateManagerReplicatorSettings
    {
        /// <summary>
        /// Gets or sets how long the replicator waits after it transmits a message from the primary to the secondary for the secondary to acknowledge that it has received the message.
        /// </summary>
        /// <value>
        /// The default is 5 seconds.
        /// </value>
        public TimeSpan? RetryInterval { get; set; }

        /// <summary>
        /// Gets or sets the amount of time that the replicator waits after receiving an operation before sending back an acknowledgment.
        /// </summary>
        /// <value>
        /// The default is 5 milliseconds.
        /// </value>
        // todo: documented default of 5 milliseconds disagrees with the public Reliable Services configuration documentation (https://learn.microsoft.com/azure/service-fabric/service-fabric-reliable-services-configuration#replicator-configuration), which lists the BatchAcknowledgementInterval default as 0.015 seconds (15 milliseconds); the current runtime contract cannot be verified from this repository
        public TimeSpan? BatchAcknowledgementInterval { get; set; }

        /// <summary>
        /// Gets or sets the address in <c>{ip}:{port}</c> format that this replicator uses when communicating with other replicators.
        /// </summary>
        /// <value>
        /// The default is <c>"localhost:0"</c>, which picks a dynamic port number at runtime.
        /// </value>
        /// <remarks>
        /// If the replicator runs inside a container, set <see cref="ReplicatorListenAddress"/> and <see cref="ReplicatorPublishAddress"/> instead.
        /// </remarks>
        public string ReplicatorAddress { get; set; }

        /// <summary>
        /// Gets or sets the address in <c>{ip}:{port}</c> format that this replicator uses to receive information from other replicators.
        /// </summary>
        /// <value>
        /// The default is <c>"localhost:0"</c>, which picks a dynamic port number at runtime.
        /// </value>
        /// <remarks>
        /// The <c>{ip}</c> part of the listen address can be obtained from <see cref="CodePackageActivationContext.ServiceListenAddress"/>.
        /// </remarks>
        public string ReplicatorListenAddress { get; set; }

        /// <summary>
        /// Gets or sets the address in <c>{ip}:{port}</c> format that this replicator uses to send information to other replicators.
        /// </summary>
        /// <value>
        /// The default is <c>"localhost:0"</c>, which picks a dynamic port number at runtime.
        /// </value>
        /// <remarks>
        /// The <c>{ip}</c> part of the publish address can be obtained from <see cref="CodePackageActivationContext.ServicePublishAddress"/>.
        /// </remarks>
        public string ReplicatorPublishAddress { get; set; }

        /// <summary>
        /// Gets or sets the security credentials for securing the traffic between replicators.
        /// </summary>
        public SecurityCredentials SecurityCredentials { get; set; }

        /// <summary>
        /// Gets or sets the initial size of the copy operation queue inside the replicator.
        /// </summary>
        /// <value>
        /// The default is 64.
        /// </value>
        /// <remarks>
        /// The value is the number of operations in the copy operation queue and must be a power of 2.
        /// </remarks>
        public long? InitialCopyQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum size of the copy operation queue inside the replicator.
        /// </summary>
        /// <value>
        /// The default is 1024.
        /// </value>
        /// <remarks>
        /// The value is the maximum number of operations in the copy operation queue and must be a power of 2.
        /// </remarks>
        public long? MaxCopyQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum replication message size.
        /// </summary>
        /// <value>
        /// The default is 50 MB.
        /// </value>
        /// <remarks>
        /// The value is specified in bytes.
        /// </remarks>
        public long? MaxReplicationMessageSize { get; set; }

        /// <summary>
        /// Gets or sets the initial size of the primary replication queue.
        /// </summary>
        /// <value>
        /// The default is 64.
        /// </value>
        /// <remarks>
        /// The value is the number of operations in the primary replication queue and must be a power of 2.
        /// </remarks>
        public long? InitialPrimaryReplicationQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum size of the primary replication queue.
        /// </summary>
        /// <value>
        /// The default is 8192.
        /// </value>
        /// <remarks>
        /// The value is the maximum number of operations in the primary replication queue and must be a power of 2 and greater than 64.
        /// </remarks>
        public long? MaxPrimaryReplicationQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum memory size of the primary replication queue.
        /// </summary>
        /// <value>
        /// The default is 0, which means there is no memory limit.
        /// </value>
        /// <remarks>
        /// The value is specified in bytes.
        /// </remarks>
        public long? MaxPrimaryReplicationQueueMemorySize { get; set; }

        /// <summary>
        /// Gets or sets the initial size of the secondary replication queue.
        /// </summary>
        /// <value>
        /// The default is 64.
        /// </value>
        /// <remarks>
        /// The value is the number of operations in the secondary replication queue and must be a power of 2.
        /// </remarks>
        public long? InitialSecondaryReplicationQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum size of the secondary replication queue.
        /// </summary>
        /// <value>
        /// The default is 16384.
        /// </value>
        /// <remarks>
        /// The value is the maximum number of operations in the secondary replication queue and must be a power of 2 and greater than 64.
        /// </remarks>
        public long? MaxSecondaryReplicationQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum memory size of the secondary replication queue.
        /// </summary>
        /// <value>
        /// The default is 0, which means there is no memory limit.
        /// </value>
        /// <remarks>
        /// The value is specified in bytes.
        /// </remarks>
        public long? MaxSecondaryReplicationQueueMemorySize { get; set; }

        /// <summary>
        /// Gets or sets the GUID identifier for the log container shared by a number of replicas on the node including this one.
        /// </summary>
        /// <value>
        /// The default is an empty string, which causes the replicator to use the global shared log for the node.
        /// </value>
        public string SharedLogId { get; set; }

        /// <summary>
        /// Gets or sets the full pathname to the log container shared by a number of replicas on the node including this one.
        /// </summary>
        /// <value>
        /// The default is an empty string, which causes the replicator to use the global shared log for the node.
        /// </value>
        public string SharedLogPath { get; set; }

        /// <summary>
        /// Gets or sets the maximum stream size.
        /// </summary>
        /// <remarks>
        /// The value is specified in MB. This property is deprecated.
        /// </remarks>
        public int? MaxStreamSizeInMB { get; set; }

        /// <summary>
        /// Gets or sets the amount of extra persistent storage space reserved for the replicator associated with this replica.
        /// </summary>
        /// <value>
        /// The default is 4.
        /// </value>
        /// <remarks>
        /// The value is specified in KB and must be a multiple of 4.
        /// </remarks>
        public int? MaxMetadataSizeInKB { get; set; }

        /// <summary>
        /// Gets or sets the largest record size that the replicator may write for the log associated with this replica.
        /// </summary>
        /// <value>
        /// The default is 1024.
        /// </value>
        /// <remarks>
        /// The value is specified in KB and must be a multiple of 4 and greater than 16.
        /// </remarks>
        public int? MaxRecordSizeInKB { get; set; }

        /// <summary>
        /// Gets or sets the maximum write queue depth that the core logger can use for the log associated with this replica.
        /// </summary>
        /// <value>
        /// The default is 0.
        /// </value>
        /// <remarks>
        /// The value is the maximum number of bytes that can be outstanding during core logger updates. It may be 0 to let the core logger compute an appropriate value or a multiple of 4. The value is specified in KB.
        /// </remarks>
        public int? MaxWriteQueueDepthInKB { get; set; }

        /// <summary>
        /// Gets or sets the log usage threshold above which a checkpoint is initiated.
        /// </summary>
        /// <value>
        /// The default is 50.
        /// </value>
        /// <remarks>
        /// The value is specified in MB.
        /// </remarks>
        public int? CheckpointThresholdInMB { get; set; }

        /// <summary>
        /// Gets or sets the maximum size for an accumulated backup log across backups.
        /// </summary>
        /// <value>
        /// The default is 800.
        /// </value>
        /// <remarks>
        /// An incremental backup request fails when the backup logs it generates would cause the total amount of logs accumulated since the last full backup to exceed this value. In that case, take a full backup. The value is specified in MB.
        /// </remarks>
        public int? MaxAccumulatedBackupLogSizeInMB { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether the replicator is optimized for local SSD storage.
        /// </summary>
        /// <remarks>
        /// This property is deprecated.
        /// </remarks>
        public bool? OptimizeForLocalSSD { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether the log should be optimized to use less disk space at the cost of IO performance.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the log uses less disk space at the cost of IO performance; otherwise, <see langword="false"/>, in which case the log uses more disk space but has better IO performance. The default is <see langword="true"/>.
        /// </value>
        public bool? OptimizeLogForLowerDiskUsage { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether the secondary replicator should clear the in-memory queue after acknowledging operations to the primary.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the secondary replicator clears the in-memory queue after acknowledging operations (after they are flushed to disk); otherwise, <see langword="false"/>. The default is <see langword="false"/>.
        /// </value>
        /// <remarks>
        /// Setting this to <see langword="true"/> can result in additional disk reads on the new primary when catching up replicas after a failover.
        /// </remarks>
        public bool? SecondaryClearAcknowledgedOperations { get; set; }

        /// <summary>
        /// Gets or sets the interval after which the replicator sends a warning health report indicating that the API is slow and taking longer than expected.
        /// </summary>
        /// <value>
        /// The default is 5 minutes.
        /// </value>
        public TimeSpan? SlowApiMonitoringDuration { get; set; }

        /// <summary>
        /// Gets or sets the minimum log size.
        /// </summary>
        /// <value>
        /// The default is 0.
        /// </value>
        /// <remarks>
        /// A truncation is not initiated if it would reduce the size of the log below this value. The value is specified in MB.
        /// </remarks>
        public int? MinLogSizeInMB { get; set; }

        /// <summary>
        /// Gets or sets the truncation threshold factor.
        /// </summary>
        /// <value>
        /// The default is 2.
        /// </value>
        /// <remarks>
        /// A truncation is initiated when log usage exceeds this value times <see cref="MinLogSizeInMB"/>.
        /// </remarks>
        public int? TruncationThresholdFactor { get; set; }

        /// <summary>
        /// Gets or sets the throttling threshold factor.
        /// </summary>
        /// <value>
        /// The default is 4.
        /// </value>
        /// <remarks>
        /// Throttling is initiated when log usage exceeds the maximum of this value times <see cref="MinLogSizeInMB"/>
        /// and this value times <see cref="CheckpointThresholdInMB"/>.
        /// The throttling threshold must be greater than the truncation threshold.
        /// </remarks>
        public int? ThrottlingThresholdFactor { get; set; }

#if NETFRAMEWORK
        // 12529905 - Disable new configuration for LogTruncationIntervalSeconds in CoreCLR
        /// <summary>
        /// Gets or sets a time interval at which log truncation will be initiated.
        /// </summary>
        public int? LogTruncationIntervalSeconds { get; set; }

        /// <summary>
        /// Configuration that enables incremental backups to be chained across primary replicas.
        /// When this flag is turned off, a primary replica can only take an incremental backup if it took the last backup at the same epoch.
        /// When this flag is turned on, a primary replica can take an incremental backup whether or not it was the replica that took the last backup with the same dataloss number.
        /// </summary>
        internal bool? EnableIncrementalBackupsAcrossReplicas { get; set; }

        /// <summary>
        /// Controls if send window size for primary queues should be in bytes of number of messages
        /// The default is false
        /// </summary>
        internal bool? EnableSendWindowSizeInBytes { get; set; }

        /// <summary>
        /// If enableSendWindowSizeInBytes is set then specifies the amount of bytes from replication queue
        /// that can be put on wire
        /// </summary>
        internal uint? MaxReplicationQueueSendWindowSizeInBytes { get; set; }

        /// <summary>
        /// If enableSendWindowSizeInBytes is set then specifies the amount of bytes from copy queue
        /// that can be put on wire
        /// </summary>
        internal uint? MaxCopyQueueSendWindowSizeInBytes { get; set; }

        /// <summary>
        /// Controls if multiple replicas within process should use their own individual heaps or shared heap.
        /// The default is false
        /// </summary>
        internal bool? UseIndividualHeapPerReplica { get; set; }

        /// <summary>
        /// Controls the initial size of the heap owned by a replicas in a process, when UseIndividualHeapPerReplica is enabled.
        /// The default is 0
        /// </summary>
        internal uint? InitialReplicaHeapSizeInKB { get; set; }
#endif

        /// <summary>
        /// Returns a value that indicates whether the specified object is a <see cref="ReliableStateManagerReplicatorSettings"/> with equivalent V2 settings.
        /// </summary>
        /// <param name="obj">
        /// The object to compare with the current instance.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="obj"/> is a <see cref="ReliableStateManagerReplicatorSettings"/> whose V2 settings match the current instance; otherwise, <see langword="false"/>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            var arg = (ReliableStateManagerReplicatorSettings)obj;
            return InternalEquals(this, arg);
        }

        /// <inheritdoc/>
        // todo: returns reference-identity hash via base.GetHashCode() while Equals does deep value comparison
        // through InternalEquals, so two settings instances that compare equal hash differently and break
        // HashSet/Dictionary lookups in violation of the Object.GetHashCode contract
        public override int GetHashCode()
        {
            // ReSharper disable once BaseObjectGetHashCodeCallInGetHashCode
            return base.GetHashCode();
        }

        /// <summary>
        /// Returns a multi-line listing of the property values that have been set on this instance.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendFormat(Environment.NewLine);
            if (this.SharedLogId != null)
            {
                builder.AppendFormat("SharedLogId = {0}" + Environment.NewLine, this.SharedLogId);
            }

            if (this.SharedLogPath != null)
            {
                builder.AppendFormat("SharedLogPath = {0}" + Environment.NewLine, this.SharedLogPath);
            }

            if (this.MaxStreamSizeInMB.HasValue)
            {
                builder.AppendFormat("MaxStreamSizeInMB = {0}" + Environment.NewLine, this.MaxStreamSizeInMB);
            }

            if (this.MaxRecordSizeInKB.HasValue)
            {
                builder.AppendFormat("MaxRecordSizeInKB = {0}" + Environment.NewLine, this.MaxRecordSizeInKB);
            }

            if (this.MaxMetadataSizeInKB.HasValue)
            {
                builder.AppendFormat("MaxMetadataSizeInKB = {0}" + Environment.NewLine, this.MaxMetadataSizeInKB);
            }

            if (this.OptimizeForLocalSSD.HasValue)
            {
                builder.AppendFormat("OptimizeForLocalSSD = {0}" + Environment.NewLine, this.OptimizeForLocalSSD);
            }

            if (this.OptimizeLogForLowerDiskUsage.HasValue)
            {
                builder.AppendFormat("OptimizeLogForLowerDiskUsage = {0}" + Environment.NewLine, this.OptimizeLogForLowerDiskUsage);
            }

            if (this.CheckpointThresholdInMB.HasValue)
            {
                builder.AppendFormat("CheckpointThresholdInMB = {0}" + Environment.NewLine, this.CheckpointThresholdInMB);
            }

            if (this.MaxAccumulatedBackupLogSizeInMB.HasValue)
            {
                builder.AppendFormat("MaxAccumulatedBackupLogSizeInMB = {0}" + Environment.NewLine, this.MaxAccumulatedBackupLogSizeInMB);
            }

            if (this.MinLogSizeInMB.HasValue)
            {
                builder.AppendFormat("MinLogSizeInMB = {0}" + Environment.NewLine, this.MinLogSizeInMB);
            }

            if (this.TruncationThresholdFactor.HasValue)
            {
                builder.AppendFormat("TruncationThresholdFactor = {0}" + Environment.NewLine, this.TruncationThresholdFactor);
            }

            if (this.ThrottlingThresholdFactor.HasValue)
            {
                builder.AppendFormat("ThrottlingThresholdFactor = {0}" + Environment.NewLine, this.ThrottlingThresholdFactor);
            }

            if (this.SlowApiMonitoringDuration.HasValue)
            {
                builder.AppendFormat("SlowApiMonitoringDuration = {0}" + Environment.NewLine, this.SlowApiMonitoringDuration);
            }

#if NETFRAMEWORK
            // 12529905 - Disable new configuration for LogTruncationIntervalSeconds in CoreCLR
            if (this.LogTruncationIntervalSeconds.HasValue)
            {
                builder.AppendFormat("LogTruncationIntervalSeconds = {0}" + Environment.NewLine, this.LogTruncationIntervalSeconds);
            }

            if (this.EnableIncrementalBackupsAcrossReplicas.HasValue)
            {
                builder.AppendFormat("EnableIncrementalBackupsAcrossReplicas = {0}" + Environment.NewLine, this.EnableIncrementalBackupsAcrossReplicas);
            }

            if (this.EnableSendWindowSizeInBytes.HasValue)
            {
                builder.AppendFormat("EnableSendWindowSizeInBytes = {0}" + Environment.NewLine, this.EnableSendWindowSizeInBytes);
            }

            if (this.MaxReplicationQueueSendWindowSizeInBytes.HasValue)
            {
                builder.AppendFormat("MaxReplicationQueueSendWindowSizeInBytes = {0}" + Environment.NewLine, this.MaxReplicationQueueSendWindowSizeInBytes);
            }

            if (this.MaxCopyQueueSendWindowSizeInBytes.HasValue)
            {
                builder.AppendFormat("MaxCopyQueueSendWindowSizeInBytes = {0}" + Environment.NewLine, this.MaxCopyQueueSendWindowSizeInBytes);
            }

            if (this.UseIndividualHeapPerReplica.HasValue)
            {
                builder.AppendFormat("UseIndividualHeapPerReplica = {0}" + Environment.NewLine, this.UseIndividualHeapPerReplica);
            }

            if (this.InitialReplicaHeapSizeInKB.HasValue)
            {
                builder.AppendFormat("InitialReplicaHeapSizeInKB = {0}" + Environment.NewLine, this.InitialReplicaHeapSizeInKB);
            }
#endif
            return builder.ToString();
        }

        /// <summary>
        /// Checks for equality of setting values.
        /// </summary>
        /// <param name="old">Old settings.</param>
        /// <param name="updated">Updated settings.</param>
        /// <returns>
        /// TRUE if the settings are equivalent.
        /// </returns>
        private static bool InternalEquals(ReliableStateManagerReplicatorSettings old, ReliableStateManagerReplicatorSettings updated)
        {
            // compare only the V2 settings.
            var areEqual = true;

            if (!string.IsNullOrEmpty(updated.SharedLogId))
            {
                areEqual = !string.IsNullOrEmpty(old.SharedLogId) && (old.SharedLogId == updated.SharedLogId);
            }

            if (areEqual && !string.IsNullOrEmpty(updated.SharedLogPath))
            {
                areEqual = !string.IsNullOrEmpty(old.SharedLogPath) && (old.SharedLogPath == updated.SharedLogPath);
            }

            if (areEqual && updated.MaxStreamSizeInMB.HasValue)
            {
                areEqual = old.MaxStreamSizeInMB.HasValue && (old.MaxStreamSizeInMB.Value == updated.MaxStreamSizeInMB.Value);
            }

            if (areEqual && updated.MaxRecordSizeInKB.HasValue)
            {
                areEqual = old.MaxRecordSizeInKB.HasValue && (old.MaxRecordSizeInKB.Value == updated.MaxRecordSizeInKB.Value);
            }

            if (areEqual && updated.MaxMetadataSizeInKB.HasValue)
            {
                areEqual = old.MaxMetadataSizeInKB.HasValue && (old.MaxMetadataSizeInKB.Value == updated.MaxMetadataSizeInKB.Value);
            }

            if (areEqual && updated.OptimizeForLocalSSD.HasValue)
            {
                areEqual = old.OptimizeForLocalSSD.HasValue && (old.OptimizeForLocalSSD.Value == updated.OptimizeForLocalSSD.Value);
            }

            if (areEqual && updated.OptimizeLogForLowerDiskUsage.HasValue)
            {
                areEqual = old.OptimizeLogForLowerDiskUsage.HasValue && (old.OptimizeLogForLowerDiskUsage.Value == updated.OptimizeLogForLowerDiskUsage.Value);
            }

            if (areEqual && updated.CheckpointThresholdInMB.HasValue)
            {
                areEqual = old.CheckpointThresholdInMB.HasValue && (old.CheckpointThresholdInMB.Value == updated.CheckpointThresholdInMB.Value);
            }

            if (areEqual && updated.MaxAccumulatedBackupLogSizeInMB.HasValue)
            {
                areEqual = old.MaxAccumulatedBackupLogSizeInMB.HasValue && (old.MaxAccumulatedBackupLogSizeInMB.Value == updated.MaxAccumulatedBackupLogSizeInMB.Value);
            }

            if (areEqual && updated.MinLogSizeInMB.HasValue)
            {
                areEqual = old.MinLogSizeInMB.HasValue && (old.MinLogSizeInMB.Value == updated.MinLogSizeInMB.Value);
            }

            if (areEqual && updated.TruncationThresholdFactor.HasValue)
            {
                areEqual = old.TruncationThresholdFactor.HasValue && (old.TruncationThresholdFactor.Value == updated.TruncationThresholdFactor.Value);
            }

            if (areEqual && updated.ThrottlingThresholdFactor.HasValue)
            {
                areEqual = old.ThrottlingThresholdFactor.HasValue && (old.ThrottlingThresholdFactor.Value == updated.ThrottlingThresholdFactor.Value);
            }

            if (areEqual && updated.SlowApiMonitoringDuration.HasValue)
            {
                areEqual = old.SlowApiMonitoringDuration.HasValue && (old.SlowApiMonitoringDuration == updated.SlowApiMonitoringDuration);
            }

#if NETFRAMEWORK
            // 12529905 - Disable new configuration for LogTruncationIntervalSeconds in CoreCLR
            if (areEqual && updated.LogTruncationIntervalSeconds.HasValue)
            {
                areEqual = old.LogTruncationIntervalSeconds.HasValue && (old.LogTruncationIntervalSeconds == updated.LogTruncationIntervalSeconds);
            }

            if (areEqual && updated.EnableIncrementalBackupsAcrossReplicas.HasValue)
            {
                areEqual = old.EnableIncrementalBackupsAcrossReplicas.HasValue && (old.EnableIncrementalBackupsAcrossReplicas == updated.EnableIncrementalBackupsAcrossReplicas);
            }

            if (areEqual && updated.EnableSendWindowSizeInBytes.HasValue)
            {
                areEqual = old.EnableSendWindowSizeInBytes.HasValue && (old.EnableSendWindowSizeInBytes == updated.EnableSendWindowSizeInBytes);
            }

            if (areEqual && updated.MaxReplicationQueueSendWindowSizeInBytes.HasValue)
            {
                areEqual = old.MaxReplicationQueueSendWindowSizeInBytes.HasValue && (old.MaxReplicationQueueSendWindowSizeInBytes == updated.MaxReplicationQueueSendWindowSizeInBytes);
            }

            if (areEqual && updated.MaxCopyQueueSendWindowSizeInBytes.HasValue)
            {
                areEqual = old.MaxCopyQueueSendWindowSizeInBytes.HasValue && (old.MaxCopyQueueSendWindowSizeInBytes == updated.MaxCopyQueueSendWindowSizeInBytes);
            }

            if (areEqual && updated.UseIndividualHeapPerReplica.HasValue)
            {
                areEqual = old.UseIndividualHeapPerReplica.HasValue && (old.UseIndividualHeapPerReplica == updated.UseIndividualHeapPerReplica);
            }

            if (areEqual && updated.InitialReplicaHeapSizeInKB.HasValue)
            {
                areEqual = old.InitialReplicaHeapSizeInKB.HasValue && (old.InitialReplicaHeapSizeInKB == updated.InitialReplicaHeapSizeInKB);
            }
#endif
            return areEqual;
        }
    }
}
