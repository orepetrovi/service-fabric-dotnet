// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using Fuzzy;
using Inspector;
using Moq;
using Xunit;

public abstract class WcfGlobalErrorHandlerTest
{
    readonly WcfGlobalErrorHandler sut;

    // Constructor parameters
    readonly ChannelDispatcher dispatcher;

    // Test fixture
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);
    readonly Mock<IChannelListener> listener = new() { DefaultValue = DefaultValue.Mock };

    protected WcfGlobalErrorHandlerTest()
    {
        listener.Setup(l => l.State).Returns(CommunicationState.Faulted);
        dispatcher = Type<ChannelDispatcher>.Uninitialized();
        dispatcher.Field<IChannelListener>().Set(listener.Object);
        sut = new WcfGlobalErrorHandler(dispatcher);
    }

    public sealed class HandleError : WcfGlobalErrorHandlerTest
    {
        [Fact]
        public void ReturnsFalseForFaultException()
        {
            var error = new FaultException(fuzzy.String());
            Assert.False(sut.HandleError(error));
        }

        [Fact]
        public void ReturnsTrueForNonFaultException()
        {
            var error = new InvalidOperationException(fuzzy.String());
            Assert.True(sut.HandleError(error));
        }
    }

    public sealed class ProvideFault : WcfGlobalErrorHandlerTest
    {
        [Fact]
        public void ProducesFaultWithRetryCodeAndExceptionTypeName()
        {
            var error = new InvalidOperationException(fuzzy.String());
            Message fault = null;

            sut.ProvideFault(error, MessageVersion.Default, ref fault);

            Assert.NotNull(fault);
            MessageFault messageFault = MessageFault.CreateFault(fault, int.MaxValue);
            Assert.Equal(WcfRemoteExceptionInformation.FaultCodeName, messageFault.Code.Name);
            Assert.Equal(WcfRemoteExceptionInformation.FaultSubCodeRetryName, messageFault.Code.SubCode.Name);
            Assert.Equal(error.GetType().FullName, messageFault.Reason.ToString());
        }

        [Fact]
        public void DoesNotProduceFaultForFaultException()
        {
            var error = new FaultException(fuzzy.String());
            Message fault = null;

            sut.ProvideFault(error, MessageVersion.Default, ref fault);

            Assert.Null(fault);
        }

        [Fact]
        public void DoesNotProduceFaultWhenListenerIsOpened()
        {
            listener.Setup(l => l.State).Returns(CommunicationState.Opened);
            var error = new InvalidOperationException(fuzzy.String());
            Message fault = null;

            sut.ProvideFault(error, MessageVersion.Default, ref fault);

            Assert.Null(fault);
        }
    }
}
