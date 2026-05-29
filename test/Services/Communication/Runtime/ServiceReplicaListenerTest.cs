// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using Fuzzy;
using Inspector;
using Microsoft.ServiceFabric.Diagnostics.Tracing;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.Runtime
{
    public abstract class ServiceReplicaListenerTest
    {
        readonly ServiceReplicaListener sut;

        // Constructor parameters
        readonly Func<StatefulServiceContext, ICommunicationListener> createCommunicationListener = Mock.Of<Func<StatefulServiceContext, ICommunicationListener>>();
        readonly string name = fuzzy.String();
        readonly bool listenOnSecondary = fuzzy.Boolean();

        static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

        ServiceReplicaListenerTest() =>
            sut = new ServiceReplicaListener(createCommunicationListener, name, listenOnSecondary);

        public sealed class Constructor : ServiceReplicaListenerTest
        {
            [Fact]
            public void ThrowsArgumentNullExceptionWhenCreateCommunicationListenerIsNull()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => new ServiceReplicaListener(null, name, listenOnSecondary));
                Assert.Equal(nameof(createCommunicationListener), exception.ParamName);
            }

            [Fact]
            public void ThrowsArgumentNullExceptionWhenNameIsNull()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => new ServiceReplicaListener(createCommunicationListener, null, listenOnSecondary));
                Assert.Equal(nameof(name), exception.ParamName);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void InitializesPropertiesWithGivenArguments(bool listenOnSecondary)
            {
                var sut = new ServiceReplicaListener(createCommunicationListener, name, listenOnSecondary);
                Assert.Same(createCommunicationListener, sut.CreateCommunicationListener);
                Assert.Same(name, sut.Name);
                Assert.Equal(listenOnSecondary, sut.ListenOnSecondary);
            }

            [Fact]
            public void InitializesPropertiesWithDefaultArgumentValues()
            {
                var sut = new ServiceReplicaListener(createCommunicationListener);
                Assert.Same(ServiceReplicaListener.DefaultName, sut.Name);
                Assert.False(sut.ListenOnSecondary);
            }
        }

        public sealed class Instantiate : ServiceReplicaListenerTest
        {
            // Method parameters
            readonly StatefulServiceContext context = fuzzy.StatefulServiceContext();

            readonly ICommunicationListener listener = Mock.Of<ICommunicationListener>();

            public Instantiate() =>
                _ = Mock.Get(createCommunicationListener).Setup(_ => _(context)).Returns(listener);

            [Fact]
            public void ReturnsTracingListener()
            {
                CommunicationListenerInfo actual = ServiceReplicaListener.Instantiate(sut, context);

                Assert.Same(name, actual.Name);
                var tracer = (TracingCommunicationListener)actual.Listener;
                var original = tracer.Field<CommunicationListenerInfo>().Value;
                Assert.Equal(new CommunicationListenerInfo(name, listener), original);
                var trace = (Trace)tracer.Field<ITrace>().Value;
                Assert.Equal(new Trace(typeof(ServiceReplicaListener), context, ServiceEventSource.Instance), trace);
            }

            [Fact]
            public void ReturnsTracingListenerWithDefaultName()
            {
                var sut = new ServiceReplicaListener(createCommunicationListener);

                CommunicationListenerInfo actual = ServiceReplicaListener.Instantiate(sut, context);

                string expectedName = "default";
                Assert.Same(expectedName, actual.Name);
                var tracer = (TracingCommunicationListener)actual.Listener;
                var original = tracer.Field<CommunicationListenerInfo>().Value;
                Assert.Equal(new CommunicationListenerInfo(expectedName, listener), original);
                var trace = (Trace)tracer.Field<ITrace>().Value;
                Assert.Equal(new Trace(typeof(ServiceReplicaListener), context, ServiceEventSource.Instance), trace);
            }

            [Fact]
            public void ReturnsNullWhenCreateCommunicationListenerReturnsNull()
            {
                _ = Mock.Get(createCommunicationListener).Setup(_ => _(context)).Returns(default(ICommunicationListener));
                Assert.Null(ServiceReplicaListener.Instantiate(sut, context));
            }

            [Fact]
            public void ThrowsArgumentNullExceptionWhenListenerIsNull()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => ServiceReplicaListener.Instantiate(null, context));
                Assert.Equal(nameof(listener), exception.ParamName);
            }
        }
    }
}
