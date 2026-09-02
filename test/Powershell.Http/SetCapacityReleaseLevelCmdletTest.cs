// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Security;
using Microsoft.ServiceFabric.Client;
using Microsoft.ServiceFabric.Common;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Powershell.Http;

public abstract class SetCapacityReleaseLevelCmdletTest
{
    const string CommandName = "Set-SFCapacityReleaseLevel";

    public sealed class CmdletAttributeTest : SetCapacityReleaseLevelCmdletTest
    {
        [Fact]
        public void EnablesHighImpactShouldProcess()
        {
            CmdletAttribute metadata = Assert.Single(
                typeof(SetCapacityReleaseLevelCmdlet).GetCustomAttributes<CmdletAttribute>());

            Assert.True(metadata.SupportsShouldProcess);
            Assert.Equal(ConfirmImpact.High, metadata.ConfirmImpact);
        }
    }

    public sealed class ProcessRecordInternal : SetCapacityReleaseLevelCmdletTest
    {
        [Theory]
        [InlineData(CapacityReleaseLevel.None)]
        [InlineData(CapacityReleaseLevel.Minor)]
        [InlineData(CapacityReleaseLevel.Major)]
        public void WhatIfDescribesLevelAndDoesNotInvokeClient(CapacityReleaseLevel level)
        {
            Mock<IServiceFabricClient> client = new(MockBehavior.Strict);
            RecordingHost host = new();
            InitialSessionState state = InitialSessionState.CreateDefault();
            state.Commands.Add(new SessionStateCmdletEntry(
                CommandName,
                typeof(SetCapacityReleaseLevelCmdlet),
                null));

            using Runspace runspace = RunspaceFactory.CreateRunspace(host, state);
            runspace.Open();
            runspace.SessionStateProxy.SetVariable("SFHttpClusterConnection", client.Object);

            using System.Management.Automation.PowerShell powershell = System.Management.Automation.PowerShell.Create();
            powershell.Runspace = runspace;
            _ = powershell
                .AddCommand(CommandName)
                .AddParameter(nameof(SetCapacityReleaseLevelCmdlet.Level), level)
                .AddParameter("WhatIf");

            _ = powershell.Invoke();

            Assert.Empty(powershell.Streams.Error);
            Assert.Contains(
                host.Messages,
                _ => _.Contains(
                    $"Set capacity release level to '{level}'",
                    StringComparison.Ordinal)
                    && _.Contains("Service Fabric cluster", StringComparison.Ordinal));
            client.VerifyNoOtherCalls();
        }
    }

    sealed class RecordingHost : PSHost
    {
        readonly Guid instanceId = Guid.NewGuid();
        readonly RecordingHostUserInterface ui = new();

        internal IReadOnlyCollection<string> Messages => ui.Messages;

        public override CultureInfo CurrentCulture => CultureInfo.CurrentCulture;
        public override CultureInfo CurrentUICulture => CultureInfo.CurrentUICulture;
        public override Guid InstanceId => instanceId;
        public override string Name => nameof(RecordingHost);
        public override PSHostUserInterface UI => ui;
        public override Version Version { get; } = new(1, 0);

        public override void EnterNestedPrompt() =>
            throw new NotSupportedException();

        public override void ExitNestedPrompt() =>
            throw new NotSupportedException();

        public override void NotifyBeginApplication()
        {
        }

        public override void NotifyEndApplication()
        {
        }

        public override void SetShouldExit(int exitCode)
        {
        }
    }

    sealed class RecordingHostUserInterface : PSHostUserInterface
    {
        readonly List<string> messages = [];

        internal IReadOnlyCollection<string> Messages => messages;

        public override PSHostRawUserInterface RawUI => null;

        public override Dictionary<string, PSObject> Prompt(
            string caption,
            string message,
            Collection<FieldDescription> descriptions) =>
            [];

        public override int PromptForChoice(
            string caption,
            string message,
            Collection<ChoiceDescription> choices,
            int defaultChoice) =>
            defaultChoice;

        public override PSCredential PromptForCredential(
            string caption,
            string message,
            string userName,
            string targetName) =>
            null;

        public override PSCredential PromptForCredential(
            string caption,
            string message,
            string userName,
            string targetName,
            PSCredentialTypes allowedCredentialTypes,
            PSCredentialUIOptions options) =>
            null;

        public override string ReadLine() =>
            string.Empty;

        public override SecureString ReadLineAsSecureString() =>
            new();

        public override void Write(string value) =>
            messages.Add(value);

        public override void Write(
            ConsoleColor foregroundColor,
            ConsoleColor backgroundColor,
            string value) =>
            messages.Add(value);

        public override void WriteDebugLine(string message) =>
            messages.Add(message);

        public override void WriteErrorLine(string value) =>
            messages.Add(value);

        public override void WriteLine(string value) =>
            messages.Add(value);

        public override void WriteProgress(long sourceId, ProgressRecord record)
        {
        }

        public override void WriteVerboseLine(string message) =>
            messages.Add(message);

        public override void WriteWarningLine(string message) =>
            messages.Add(message);
    }
}
