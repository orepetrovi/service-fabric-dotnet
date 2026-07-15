// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.ServiceFabric.Services;

public abstract class TelemetryConstantsTest
{
    public sealed class ASPNetCoreCommunicationListener : TelemetryConstantsTest
    {
        [Fact]
        public void IsAspNetCore() =>
            Assert.Equal("ASP.NET Core", TelemetryConstants.ASPNetCoreCommunicationListener);
    }

    public sealed class ClusterOSLinux : TelemetryConstantsTest
    {
        [Fact]
        public void IsLinux() =>
            Assert.Equal("Linux", TelemetryConstants.ClusterOSLinux);
    }

    public sealed class ClusterOSWindows : TelemetryConstantsTest
    {
        [Fact]
        public void IsWindows() =>
            Assert.Equal("Windows", TelemetryConstants.ClusterOSWindows);
    }

    public sealed class CommunicationListenerUsageEventName : TelemetryConstantsTest
    {
        [Fact]
        public void IsTelemetryEventsCommunicationListenerUsageEvent() =>
            Assert.Equal("TelemetryEvents.CommunicationListenerUsageEvent", TelemetryConstants.CommunicationListenerUsageEventName);
    }

    public sealed class DotNetFramework : TelemetryConstantsTest
    {
        [Fact]
        public void IsDotNetFramework() =>
            Assert.Equal("DotNetFramework", TelemetryConstants.DotNetFramework);
    }

    public sealed class DotNetStandard : TelemetryConstantsTest
    {
        [Fact]
        public void IsDotNetStandard() =>
            Assert.Equal("DotNetStandard", TelemetryConstants.DotNetStandard);
    }

    public sealed class FabricTransportCommunicationListener : TelemetryConstantsTest
    {
        [Fact]
        public void IsFabricTransport() =>
            Assert.Equal("FabricTransport", TelemetryConstants.FabricTransportCommunicationListener);
    }

    public sealed class LifecycleEventClosed : TelemetryConstantsTest
    {
        [Fact]
        public void IsClosed() =>
            Assert.Equal("Closed", TelemetryConstants.LifecycleEventClosed);
    }

    public sealed class LifecycleEventOpened : TelemetryConstantsTest
    {
        [Fact]
        public void IsOpened() =>
            Assert.Equal("Opened", TelemetryConstants.LifecycleEventOpened);
    }

    public sealed class OsType : TelemetryConstantsTest
    {
        [Fact]
        public void MatchesCurrentOperatingSystem()
        {
#if NET
            string expected = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? TelemetryConstants.ClusterOSWindows
                : TelemetryConstants.ClusterOSLinux;
#else
            string expected = TelemetryConstants.ClusterOSWindows;
#endif
            Assert.Equal(expected, TelemetryConstants.OsType);
        }
    }

    public sealed class RemotingVersionV1 : TelemetryConstantsTest
    {
        [Fact]
        public void IsV1() =>
            Assert.Equal("V1", TelemetryConstants.RemotingVersionV1);
    }

    public sealed class RemotingVersionV2 : TelemetryConstantsTest
    {
        [Fact]
        public void IsV2() =>
            Assert.Equal("V2", TelemetryConstants.RemotingVersionV2);
    }

    public sealed class RuntimePlatform : TelemetryConstantsTest
    {
        [Fact]
        public void MatchesCurrentRuntime()
        {
#if NET
            string expected = TelemetryConstants.DotNetStandard;
#else
            string expected = TelemetryConstants.DotNetFramework;
#endif
            Assert.Equal(expected, TelemetryConstants.RuntimePlatform);
        }
    }

    public sealed class ServiceLifecycleEventName : TelemetryConstantsTest
    {
        [Fact]
        public void IsTelemetryEventsServiceLifecycleEvent() =>
            Assert.Equal("TelemetryEvents.ServiceLifecycleEvent", TelemetryConstants.ServiceLifecycleEventName);
    }

    public sealed class ServiceRemotingUsageEventName : TelemetryConstantsTest
    {
        [Fact]
        public void IsTelemetryEventsServiceRemotingUsageEvent() =>
            Assert.Equal("TelemetryEvents.ServiceRemotingUsageEvent", TelemetryConstants.ServiceRemotingUsageEventName);
    }

    public sealed class StatefulServiceKind : TelemetryConstantsTest
    {
        [Fact]
        public void IsStatefulService() =>
            Assert.Equal("StatefulService", TelemetryConstants.StatefulServiceKind);
    }

    public sealed class StatelessServiceKind : TelemetryConstantsTest
    {
        [Fact]
        public void IsStatelessService() =>
            Assert.Equal("StatelessService", TelemetryConstants.StatelessServiceKind);
    }

    public sealed class WCFCommunicationListener : TelemetryConstantsTest
    {
        [Fact]
        public void IsWCF() =>
            Assert.Equal("WCF", TelemetryConstants.WCFCommunicationListener);
    }
}
