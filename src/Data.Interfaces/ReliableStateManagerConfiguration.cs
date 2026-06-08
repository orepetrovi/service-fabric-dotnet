// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the configuration used to create an <see cref="IReliableStateManager"/>.
    /// </summary>
    /// <remarks>
    /// Replicator settings come from one of two mutually exclusive sources: pass section names to load them from
    /// a Settings.xml config package at runtime, or pass a <see cref="ReliableStateManagerReplicatorSettings"/>
    /// instance to supply them programmatically.
    /// </remarks>
    public class ReliableStateManagerConfiguration
    {
        private const string DefaultConfigPackageName = "Config";
        private const string DefaultReplicatorSecuritySectionName = "ReplicatorSecurityConfig";
        private const string DefaultReplicatorSettingsSectionName = "ReplicatorConfig";

        /// <summary>
        /// Initializes a new instance of the <see cref="ReliableStateManagerConfiguration"/> class.
        /// </summary>
        /// <param name="configPackageName">The name of the config package whose Settings.xml provides replicator security and replicator settings.</param>
        /// <param name="replicatorSecuritySectionName">The name of the Settings.xml section that provides replicator security settings.</param>
        /// <param name="replicatorSettingsSectionName">The name of the Settings.xml section that provides replicator settings.</param>
        /// <param name="onInitializeStateSerializersEvent">A callback that registers custom state serializers via <see cref="IReliableStateManager.TryAddStateSerializer{T}(IStateSerializer{T})"/>.</param>
        public ReliableStateManagerConfiguration(
            string configPackageName = DefaultConfigPackageName,
            string replicatorSecuritySectionName = DefaultReplicatorSecuritySectionName,
            string replicatorSettingsSectionName = DefaultReplicatorSettingsSectionName,
            Func<Task> onInitializeStateSerializersEvent = null)
            : this(null, configPackageName, replicatorSecuritySectionName, replicatorSettingsSectionName, onInitializeStateSerializersEvent)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReliableStateManagerConfiguration"/> class.
        /// </summary>
        /// <param name="replicatorSettings">The replicator settings used by the <see cref="IReliableStateManager"/>.</param>
        /// <param name="onInitializeStateSerializersEvent">A callback that registers custom state serializers via <see cref="IReliableStateManager.TryAddStateSerializer{T}(IStateSerializer{T})"/>.</param>
        public ReliableStateManagerConfiguration(
            ReliableStateManagerReplicatorSettings replicatorSettings,
            Func<Task> onInitializeStateSerializersEvent = null)
            : this(replicatorSettings, null, null, null, onInitializeStateSerializersEvent)
        {
        }

        private ReliableStateManagerConfiguration(
            ReliableStateManagerReplicatorSettings replicatorSettings,
            string configPackageName,
            string replicatorSecuritySectionName,
            string replicatorSettingsSectionName,
            Func<Task> onInitializeStateSerializersEvent)
        {
            this.ReplicatorSettings = replicatorSettings;
            this.ConfigPackageName = configPackageName;
            this.ReplicatorSecuritySectionName = replicatorSecuritySectionName;
            this.ReplicatorSettingsSectionName = replicatorSettingsSectionName;
            this.OnInitializeStateSerializersEvent = onInitializeStateSerializersEvent ?? (() => Task.FromResult(true));
        }

        /// <summary>
        /// Gets the replicator settings.
        /// </summary>
        /// <value>The replicator settings supplied to the constructor, or <see langword="null"/> if none were supplied.</value>
        public ReliableStateManagerReplicatorSettings ReplicatorSettings { get; private set; }

        /// <summary>
        /// Gets the name of the config package whose Settings.xml provides replicator settings and replicator
        /// security settings.
        /// </summary>
        /// <value>The configuration package name. The default is <c>"Config"</c> when none is supplied to the constructor. Returns <see langword="null"/> when a <see cref="ReliableStateManagerReplicatorSettings"/> instance was supplied to the constructor instead.</value>
        public string ConfigPackageName { get; private set; }

        /// <summary>
        /// Gets the replicator security settings section name.
        /// </summary>
        /// <value>The section name. The default is <c>"ReplicatorSecurityConfig"</c> when none is supplied to the constructor. Returns <see langword="null"/> when a <see cref="ReliableStateManagerReplicatorSettings"/> instance was supplied to the constructor instead.</value>
        /// <remarks>When this section is present in the Settings.xml of the config package specified by
        /// <see cref="ConfigPackageName"/>, it is used to configure replicator security settings.</remarks>
        public string ReplicatorSecuritySectionName { get; private set; }

        /// <summary>
        /// Gets the replicator settings section name.
        /// </summary>
        /// <value>The section name. The default is <c>"ReplicatorConfig"</c> when none is supplied to the constructor. Returns <see langword="null"/> when a <see cref="ReliableStateManagerReplicatorSettings"/> instance was supplied to the constructor instead.</value>
        /// <remarks>When this section is present in the Settings.xml of the config package specified by
        /// <see cref="ConfigPackageName"/>, it is used to configure replicator settings.</remarks>
        public string ReplicatorSettingsSectionName { get; private set; }

        /// <summary>
        /// Gets the callback invoked when custom state serializers can be registered.
        /// </summary>
        /// <value>The registration callback. Never <see langword="null"/>. The default is a no-op when none was supplied to the constructor.</value>
        /// <remarks>
        /// The callback should register custom serializers via
        /// <see cref="IReliableStateManager.TryAddStateSerializer{T}(IStateSerializer{T})"/>; it is the
        /// configuration-supplied alternative to calling that method from the constructor of the Stateful Service.
        /// See that method's remarks for the timing requirement that applies to both registration paths.
        /// </remarks>
        public Func<Task> OnInitializeStateSerializersEvent { get; private set; }
    }
}
