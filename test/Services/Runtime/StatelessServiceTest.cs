// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Inspector;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Runtime;

public abstract class StatelessServiceTest
{
    readonly TestService sut;

    // Constructor parameters
    readonly StatelessServiceContext serviceContext = fuzzy.StatelessServiceContext();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    StatelessServiceTest() =>
        sut = new TestService(serviceContext);

    public sealed class Constructor : StatelessServiceTest, IDisposable
    {
        // Installed before base field initializers and base ctor body so that the event raised by sut
        // construction is captured.
        readonly EventSourceTest<ServiceEventSource> events = InstallEventSource();

        void IDisposable.Dispose() => events.Dispose();

        [Fact]
        public void InitializesProperties()
        {
            Assert.Same(serviceContext, sut.Context);
            Assert.Null(sut.GetPartitionForTest());
            Assert.Empty(sut.GetAddressesForTest());
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenServiceContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TestService(null));
            Assert.Equal(nameof(serviceContext), exception.ParamName);
        }

        [Fact]
        public void RaisesStatelessServiceInitializeEvent()
        {
            Assert.NotNull(events.Event);
            Assert.Equal("ServiceLifecycleEvent", events.Event.EventName);
            events.EventPayload(3, "partitionId", serviceContext.PartitionId.ToString());
            events.EventPayload(4, "replicaOrInstanceId", serviceContext.InstanceId.ToString());
            events.EventPayload(9, "lifecycleEvent", TelemetryConstants.LifecycleEventOpened);
            events.EventPayload(10, "serviceKind", TelemetryConstants.StatelessServiceKind);
        }
    }

    public sealed class CreateServiceInstanceListeners : StatelessServiceTest
    {
        [Fact]
        public void ReturnsEmptyByDefault() =>
            Assert.Empty(sut.InvokeBaseCreateServiceInstanceListeners());
    }

    public abstract class IStatelessUserServiceInstanceTest : StatelessServiceTest
    {
        private protected new readonly IStatelessUserServiceInstance sut;

        private protected IStatelessUserServiceInstanceTest() => sut = base.sut;
    }

    public sealed class IStatelessUserServiceInstance_Addresses : IStatelessUserServiceInstanceTest
    {
        readonly IReadOnlyDictionary<string, string> addresses = fuzzy.Dictionary(fuzzy.String, fuzzy.String);

        [Fact]
        public void UpdatesValueReturnedByGetAddresses()
        {
            sut.Addresses = addresses;
            Assert.Same(addresses, ((TestService)sut).GetAddressesForTest());
        }
    }

    public sealed class IStatelessUserServiceInstance_CreateServiceInstanceListeners : IStatelessUserServiceInstanceTest
    {
        [Fact]
        public void ForwardsToProtectedCreateServiceInstanceListeners()
        {
            IEnumerable<ServiceInstanceListener> expected = fuzzy.Array(fuzzy.ServiceInstanceListener);
            int calls = 0;
            ((TestService)sut).CreateServiceInstanceListenersHandler = () => { calls++; return expected; };

            IEnumerable<ServiceInstanceListener> actual = sut.CreateServiceInstanceListeners();

            Assert.Same(expected, actual);
            Assert.Equal(1, calls);
        }
    }

    public sealed class IStatelessUserServiceInstance_OnAbort : IStatelessUserServiceInstanceTest
    {
        [Fact]
        public void ForwardsToProtectedOnAbort()
        {
            int calls = 0;
            ((TestService)sut).OnAbortHandler = () => calls++;

            sut.OnAbort();

            Assert.Equal(1, calls);
        }
    }

    public sealed class IStatelessUserServiceInstance_OnCloseAsync : IStatelessUserServiceInstanceTest, IDisposable
    {
        // Method parameters
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        readonly EventSourceTest<ServiceEventSource> events = InstallEventSource();

        void IDisposable.Dispose() => events.Dispose();

        [Fact]
        public void ForwardsCancellationTokenToProtectedOnCloseAsync()
        {
            CancellationToken actual = default;
            int calls = 0;
            Task task = new TaskCompletionSource<int>().Task;
            ((TestService)sut).OnCloseAsyncHandler = ct => { calls++; actual = ct; return task; };

            Task result = sut.OnCloseAsync(cancellationToken);

            Assert.Same(task, result);
            Assert.Equal(cancellationToken, actual);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void RaisesStatelessServiceInstanceCloseEvent()
        {
            _ = sut.OnCloseAsync(cancellationToken);

            Assert.NotNull(events.Event);
            Assert.Equal("ServiceLifecycleEvent", events.Event.EventName);
            events.EventPayload(3, "partitionId", serviceContext.PartitionId.ToString());
            events.EventPayload(4, "replicaOrInstanceId", serviceContext.InstanceId.ToString());
            events.EventPayload(9, "lifecycleEvent", TelemetryConstants.LifecycleEventClosed);
            events.EventPayload(10, "serviceKind", TelemetryConstants.StatelessServiceKind);
        }
    }

    public sealed class IStatelessUserServiceInstance_OnOpenAsync : IStatelessUserServiceInstanceTest
    {
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public void ForwardsCancellationTokenToProtectedOnOpenAsync()
        {
            CancellationToken actual = default;
            int calls = 0;
            Task task = new TaskCompletionSource<int>().Task;
            ((TestService)sut).OnOpenAsyncHandler = ct => { calls++; actual = ct; return task; };

            Task result = sut.OnOpenAsync(cancellationToken);

            Assert.Same(task, result);
            Assert.Equal(cancellationToken, actual);
            Assert.Equal(1, calls);
        }
    }

    public sealed class IStatelessUserServiceInstance_Partition : IStatelessUserServiceInstanceTest
    {
        readonly IStatelessServicePartition partition = Mock.Of<IStatelessServicePartition>();

        [Fact]
        public void UpdatesProtectedPartitionProperty()
        {
            sut.Partition = partition;
            Assert.Same(partition, ((TestService)sut).GetPartitionForTest());
        }
    }

    public sealed class IStatelessUserServiceInstance_RunAsync : IStatelessUserServiceInstanceTest
    {
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public void ForwardsCancellationTokenToProtectedRunAsync()
        {
            CancellationToken actual = default;
            int calls = 0;
            Task task = new TaskCompletionSource<int>().Task;
            ((TestService)sut).RunAsyncHandler = ct => { calls++; actual = ct; return task; };

            Task result = sut.RunAsync(cancellationToken);

            Assert.Same(task, result);
            Assert.Equal(cancellationToken, actual);
            Assert.Equal(1, calls);
        }
    }

    public sealed class OnAbort : StatelessServiceTest
    {
        [Fact]
        public void DoesNothingByDefault() =>
            sut.InvokeBaseOnAbort();
    }

    public sealed class OnCloseAsync : StatelessServiceTest
    {
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public void ReturnsCompletedTaskByDefault()
        {
            Task actual = sut.InvokeBaseOnCloseAsync(cancellationToken);
            Assert.Equal(TaskStatus.RanToCompletion, actual.Status);
        }
    }

    public sealed class OnOpenAsync : StatelessServiceTest
    {
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public void ReturnsCompletedTaskByDefault()
        {
            Task actual = sut.InvokeBaseOnOpenAsync(cancellationToken);
            Assert.Equal(TaskStatus.RanToCompletion, actual.Status);
        }
    }

    public sealed class RunAsync : StatelessServiceTest
    {
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public void ReturnsCompletedTaskByDefault()
        {
            Task actual = sut.InvokeBaseRunAsync(cancellationToken);
            Assert.Equal(TaskStatus.RanToCompletion, actual.Status);
        }
    }

    static EventSourceTest<ServiceEventSource> InstallEventSource()
    {
        var t = new EventSourceTest<ServiceEventSource>();
        typeof(ServiceEventSource).Property<ServiceEventSource>().Set(t.Instance);
        t.EnableEvents(EventLevel.LogAlways);
        return t;
    }

    sealed class TestService(StatelessServiceContext serviceContext) : StatelessService(serviceContext)
    {
        // Hooks that override base protected virtuals when assigned; otherwise the base implementation runs.
        internal Func<CancellationToken, Task> RunAsyncHandler;
        internal Func<CancellationToken, Task> OnOpenAsyncHandler;
        internal Func<CancellationToken, Task> OnCloseAsyncHandler;
        internal Action OnAbortHandler;
        internal Func<IEnumerable<ServiceInstanceListener>> CreateServiceInstanceListenersHandler;

        protected override Task RunAsync(CancellationToken cancellation) =>
            RunAsyncHandler != null ? RunAsyncHandler(cancellation) : base.RunAsync(cancellation);

        protected override Task OnOpenAsync(CancellationToken cancellation) =>
            OnOpenAsyncHandler != null ? OnOpenAsyncHandler(cancellation) : base.OnOpenAsync(cancellation);

        protected override Task OnCloseAsync(CancellationToken cancellation) =>
            OnCloseAsyncHandler != null ? OnCloseAsyncHandler(cancellation) : base.OnCloseAsync(cancellation);

        protected override void OnAbort()
        {
            if (OnAbortHandler != null)
                OnAbortHandler();
            else
                base.OnAbort();
        }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners() =>
            CreateServiceInstanceListenersHandler != null ? CreateServiceInstanceListenersHandler() : base.CreateServiceInstanceListeners();

        internal IStatelessServicePartition GetPartitionForTest() => Partition;
        internal IReadOnlyDictionary<string, string> GetAddressesForTest() => GetAddresses();
        internal Task InvokeBaseRunAsync(CancellationToken cancellation) => base.RunAsync(cancellation);
        internal Task InvokeBaseOnOpenAsync(CancellationToken cancellation) => base.OnOpenAsync(cancellation);
        internal Task InvokeBaseOnCloseAsync(CancellationToken cancellation) => base.OnCloseAsync(cancellation);
        internal void InvokeBaseOnAbort() => base.OnAbort();
        internal IEnumerable<ServiceInstanceListener> InvokeBaseCreateServiceInstanceListeners() => base.CreateServiceInstanceListeners();
    }
}
