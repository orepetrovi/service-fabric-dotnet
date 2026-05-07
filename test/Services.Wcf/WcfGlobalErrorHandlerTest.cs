// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using Inspector;
using Moq;
using Xunit;

public abstract class WcfGlobalErrorHandlerTest
{
    readonly WcfGlobalErrorHandler sut;

    // Constructor parameters
    readonly ChannelDispatcher dispatcher;

    WcfGlobalErrorHandlerTest()
    {
        var listener = new Mock<IChannelListener> { DefaultValue = DefaultValue.Mock };
        listener.Setup(l => l.State).Returns(CommunicationState.Faulted);
        dispatcher = Type<ChannelDispatcher>.Uninitialized();
        dispatcher.Field<IChannelListener>("listener").Set(listener.Object);
        sut = new WcfGlobalErrorHandler(dispatcher);
    }

    public sealed class ProvideFault : WcfGlobalErrorHandlerTest
    {
        [Fact]
        public void ProducesFaultWithRetryCodeAndExceptionTypeName()
        {
            var error = new InvalidOperationException("test");
            Message fault = null;

            sut.ProvideFault(error, MessageVersion.Default, ref fault);

            Assert.NotNull(fault);
            var messageFault = MessageFault.CreateFault(fault, int.MaxValue);
            Assert.Equal("WcfRemoteExceptionInformation", messageFault.Code.Name);
            Assert.Equal("Retry", messageFault.Code.SubCode.Name);
            Assert.Equal(error.GetType().FullName, messageFault.Reason.ToString());
        }
    }
}
