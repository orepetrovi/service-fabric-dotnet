// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Diagnostics.Tracing;
using System.Fabric;
using Inspector;
using Microsoft.ServiceFabric.Services.Tests;
using Xunit;

namespace Microsoft.ServiceFabric.Services;

public abstract class ServiceTelemetryTest : IDisposable
{
    readonly EventSourceTest<ServiceEventSource> test = new();

    const EventKeywords Default = (EventKeywords)0x0001;

    ServiceTelemetryTest() =>
        typeof(ServiceEventSource).Property<ServiceEventSource>().Set(test.Instance);

    void IDisposable.Dispose() =>
        test.Dispose();

    public sealed class StatefulServiceInitializeEvent : ServiceTelemetryTest
    {
        readonly StatefulServiceContext context = TestMocksRepository.GetMockStatefulServiceContext();

        [Fact]
        public void PublishesServiceLifecycleEventWithOpenedAndStatefulServiceKind()
        {
            test.EnableEvents(EventLevel.LogAlways);

            ServiceTelemetry.StatefulServiceInitializeEvent(context);

            Assert.NotNull(test.Event);
            Assert.Equal(5, test.Event.EventId);
            Assert.Equal(EventLevel.Informational, test.Event.Level);
            test.EventKeywords(Default);
            Assert.Equal("ServiceLifecycleEvent", test.Event.EventName);
            Assert.Equal(11, test.Event.Payload.Count);
            test.EventPayload(0, "type", TelemetryConstants.ServiceLifecycleEventName);
            test.EventPayload(1, "clusterOsType", TelemetryConstants.OsType);
            test.EventPayload(2, "runtimePlatform", TelemetryConstants.RuntimePlatform);
            test.EventPayload(3, "partitionId", context.PartitionId.ToString());
            test.EventPayload(4, "replicaOrInstanceId", context.ReplicaId.ToString());
            test.EventPayload(5, "serviceName", context.ServiceName.OriginalString);
            test.EventPayload(6, "serviceTypeName", context.ServiceTypeName);
            test.EventPayload(7, "applicationName", context.CodePackageActivationContext.ApplicationName);
            test.EventPayload(8, "applicationTypeName", context.CodePackageActivationContext.ApplicationTypeName);
            test.EventPayload(9, "lifecycleEvent", TelemetryConstants.LifecycleEventOpened);
            test.EventPayload(10, "serviceKind", TelemetryConstants.StatefulServiceKind);
        }
    }

    public sealed class StatefulServiceReplicaCloseEvent : ServiceTelemetryTest
    {
        readonly StatefulServiceContext context = TestMocksRepository.GetMockStatefulServiceContext();

        [Fact]
        public void PublishesServiceLifecycleEventWithClosedAndStatefulServiceKind()
        {
            test.EnableEvents(EventLevel.LogAlways);

            ServiceTelemetry.StatefulServiceReplicaCloseEvent(context);

            Assert.NotNull(test.Event);
            Assert.Equal(5, test.Event.EventId);
            Assert.Equal("ServiceLifecycleEvent", test.Event.EventName);
            Assert.Equal(11, test.Event.Payload.Count);
            test.EventPayload(0, "type", TelemetryConstants.ServiceLifecycleEventName);
            test.EventPayload(1, "clusterOsType", TelemetryConstants.OsType);
            test.EventPayload(2, "runtimePlatform", TelemetryConstants.RuntimePlatform);
            test.EventPayload(3, "partitionId", context.PartitionId.ToString());
            test.EventPayload(4, "replicaOrInstanceId", context.ReplicaId.ToString());
            test.EventPayload(5, "serviceName", context.ServiceName.OriginalString);
            test.EventPayload(6, "serviceTypeName", context.ServiceTypeName);
            test.EventPayload(7, "applicationName", context.CodePackageActivationContext.ApplicationName);
            test.EventPayload(8, "applicationTypeName", context.CodePackageActivationContext.ApplicationTypeName);
            test.EventPayload(9, "lifecycleEvent", TelemetryConstants.LifecycleEventClosed);
            test.EventPayload(10, "serviceKind", TelemetryConstants.StatefulServiceKind);
        }
    }

    public sealed class StatelessServiceInitializeEvent : ServiceTelemetryTest
    {
        readonly StatelessServiceContext context = TestMocksRepository.GetMockStatelessServiceContext();

        [Fact]
        public void PublishesServiceLifecycleEventWithOpenedAndStatelessServiceKind()
        {
            test.EnableEvents(EventLevel.LogAlways);

            ServiceTelemetry.StatelessServiceInitializeEvent(context);

            Assert.NotNull(test.Event);
            Assert.Equal(5, test.Event.EventId);
            Assert.Equal("ServiceLifecycleEvent", test.Event.EventName);
            Assert.Equal(11, test.Event.Payload.Count);
            test.EventPayload(0, "type", TelemetryConstants.ServiceLifecycleEventName);
            test.EventPayload(1, "clusterOsType", TelemetryConstants.OsType);
            test.EventPayload(2, "runtimePlatform", TelemetryConstants.RuntimePlatform);
            test.EventPayload(3, "partitionId", context.PartitionId.ToString());
            test.EventPayload(4, "replicaOrInstanceId", context.InstanceId.ToString());
            test.EventPayload(5, "serviceName", context.ServiceName.OriginalString);
            test.EventPayload(6, "serviceTypeName", context.ServiceTypeName);
            test.EventPayload(7, "applicationName", context.CodePackageActivationContext.ApplicationName);
            test.EventPayload(8, "applicationTypeName", context.CodePackageActivationContext.ApplicationTypeName);
            test.EventPayload(9, "lifecycleEvent", TelemetryConstants.LifecycleEventOpened);
            test.EventPayload(10, "serviceKind", TelemetryConstants.StatelessServiceKind);
        }
    }

    public sealed class StatelessServiceInstanceCloseEvent : ServiceTelemetryTest
    {
        readonly StatelessServiceContext context = TestMocksRepository.GetMockStatelessServiceContext();

        [Fact]
        public void PublishesServiceLifecycleEventWithClosedAndStatelessServiceKind()
        {
            test.EnableEvents(EventLevel.LogAlways);

            ServiceTelemetry.StatelessServiceInstanceCloseEvent(context);

            Assert.NotNull(test.Event);
            Assert.Equal(5, test.Event.EventId);
            Assert.Equal("ServiceLifecycleEvent", test.Event.EventName);
            Assert.Equal(11, test.Event.Payload.Count);
            test.EventPayload(0, "type", TelemetryConstants.ServiceLifecycleEventName);
            test.EventPayload(1, "clusterOsType", TelemetryConstants.OsType);
            test.EventPayload(2, "runtimePlatform", TelemetryConstants.RuntimePlatform);
            test.EventPayload(3, "partitionId", context.PartitionId.ToString());
            test.EventPayload(4, "replicaOrInstanceId", context.InstanceId.ToString());
            test.EventPayload(5, "serviceName", context.ServiceName.OriginalString);
            test.EventPayload(6, "serviceTypeName", context.ServiceTypeName);
            test.EventPayload(7, "applicationName", context.CodePackageActivationContext.ApplicationName);
            test.EventPayload(8, "applicationTypeName", context.CodePackageActivationContext.ApplicationTypeName);
            test.EventPayload(9, "lifecycleEvent", TelemetryConstants.LifecycleEventClosed);
            test.EventPayload(10, "serviceKind", TelemetryConstants.StatelessServiceKind);
        }
    }

    public sealed class CommunicationListenerUsageEvent : ServiceTelemetryTest
    {
        readonly StatefulServiceContext context = TestMocksRepository.GetMockStatefulServiceContext();
        readonly string communicationListenerType = "MockCommunicationListenerType";

        [Fact]
        public void PublishesCommunicationListenerUsageEventWithGivenListenerType()
        {
            test.EnableEvents(EventLevel.LogAlways);

            ServiceTelemetry.CommunicationListenerUsageEvent(context, communicationListenerType);

            Assert.NotNull(test.Event);
            Assert.Equal(6, test.Event.EventId);
            Assert.Equal(EventLevel.Informational, test.Event.Level);
            test.EventKeywords(Default);
            Assert.Equal("CommunicationListenerUsageEvent", test.Event.EventName);
            Assert.Equal(10, test.Event.Payload.Count);
            test.EventPayload(0, "type", TelemetryConstants.CommunicationListenerUsageEventName);
            test.EventPayload(1, "clusterOsType", TelemetryConstants.OsType);
            test.EventPayload(2, "runtimePlatform", TelemetryConstants.RuntimePlatform);
            test.EventPayload(3, "partitionId", context.PartitionId.ToString());
            test.EventPayload(4, "replicaId", context.ReplicaOrInstanceId.ToString());
            test.EventPayload(5, "serviceName", context.ServiceName.OriginalString);
            test.EventPayload(6, "serviceTypeName", context.ServiceTypeName);
            test.EventPayload(7, "applicationName", context.CodePackageActivationContext.ApplicationName);
            test.EventPayload(8, "applicationTypeName", context.CodePackageActivationContext.ApplicationTypeName);
            test.EventPayload(9, "communicationListenerType", communicationListenerType);
        }
    }

    public sealed class FabricTransportServiceRemotingV2Event : ServiceTelemetryTest
    {
        readonly StatefulServiceContext context = TestMocksRepository.GetMockStatefulServiceContext();

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PublishesServiceRemotingUsageEventWithV2AndFabricTransport(bool isSecure)
        {
            test.EnableEvents(EventLevel.LogAlways);

            ServiceTelemetry.FabricTransportServiceRemotingV2Event(context, isSecure);

            Assert.NotNull(test.Event);
            Assert.Equal(7, test.Event.EventId);
            Assert.Equal(EventLevel.Informational, test.Event.Level);
            test.EventKeywords(Default);
            Assert.Equal("ServiceRemotingUsageEvent", test.Event.EventName);
            Assert.Equal(12, test.Event.Payload.Count);
            test.EventPayload(0, "type", TelemetryConstants.ServiceRemotingUsageEventName);
            test.EventPayload(1, "clusterOsType", TelemetryConstants.OsType);
            test.EventPayload(2, "runtimePlatform", TelemetryConstants.RuntimePlatform);
            test.EventPayload(3, "partitionId", context.PartitionId.ToString());
            test.EventPayload(4, "replicaId", context.ReplicaOrInstanceId.ToString());
            test.EventPayload(5, "serviceName", context.ServiceName.OriginalString);
            test.EventPayload(6, "serviceTypeName", context.ServiceTypeName);
            test.EventPayload(7, "applicationName", context.CodePackageActivationContext.ApplicationName);
            test.EventPayload(8, "applicationTypeName", context.CodePackageActivationContext.ApplicationTypeName);
            test.EventPayload(9, "isSecure", isSecure);
            test.EventPayload(10, "remotingVersion", TelemetryConstants.RemotingVersionV2);
            test.EventPayload(11, "communicationListenerType", TelemetryConstants.FabricTransportCommunicationListener);
        }
    }
}
