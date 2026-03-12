// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using Inspector;
using Microsoft.ServiceFabric.Diagnostics.Metrics;
using Microsoft.ServiceFabric.FabricTransport.Runtime;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime
{
    public abstract class FabricTransportServiceRemotingListenerTest
    {
        readonly FabricTransportServiceRemotingListener sut = Type<FabricTransportServiceRemotingListener>.Uninitialized();

        readonly IMeterProvider<TimeSpan> mockMeterProvider = Mock.Of<IMeterProvider<TimeSpan>>();
        readonly IFabricTransportListener mockFabricTransportListener = Mock.Of<IFabricTransportListener>();
        readonly IFabricTransportMessageHandler mockTransportMessageHandler = Mock.Of<IFabricTransportMessageHandler>();

        public FabricTransportServiceRemotingListenerTest()
        {
            sut.Field<IMeterProvider<TimeSpan>>().Set(mockMeterProvider);
            sut.Field<IFabricTransportListener>().Set(mockFabricTransportListener);
            sut.Field<IFabricTransportMessageHandler>().Set(mockTransportMessageHandler);
        }

        public class Dispose : FabricTransportServiceRemotingListenerTest
        {
            [Fact]
            public void AbortDisposesMeterProvider()
            {
                sut.Abort();

                Mock.Get(mockMeterProvider).Verify(m => m.Dispose(), Times.Once);
            }

            [Fact]
            public void AbortDisposesFabricTransportListener()
            {
                sut.Abort();

                Mock.Get(mockFabricTransportListener).Verify(m => m.Dispose(), Times.Once);
            }

            [Fact]
            public void AbortDisposesMessageHandler()
            {
                sut.Abort();

                Mock.Get(mockTransportMessageHandler).Verify(m => m.Dispose(), Times.Once);
            }
        }
    }
}
