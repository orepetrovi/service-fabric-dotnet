// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric;
using Fuzzy;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Communication;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Xunit;

namespace Microsoft.ServiceFabric.Actors.Client;

public abstract class FabricActorExceptionConvertorTest
{
    readonly IExceptionConvertor sut = new FabricActorExceptionConvertor();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    public sealed class TryConvertFromServiceException_ServiceException : FabricActorExceptionConvertorTest
    {
        readonly string message = fuzzy.String();

        [Fact]
        public void ProducesKnownFabricExceptionWithoutInnerException()
        {
            ServiceException serviceException = ServiceExceptionFor(typeof(DuplicateMessageException), message);

            bool result = sut.TryConvertFromServiceException(serviceException, out Exception actual);

            Assert.True(result);
            Assert.IsType<DuplicateMessageException>(actual);
            Assert.Equal(message, actual.Message);
            Assert.Null(actual.InnerException);
        }
    }

    public sealed class TryConvertFromServiceException_ServiceException_Exception : FabricActorExceptionConvertorTest
    {
        readonly string message = fuzzy.String();

        [Fact]
        public void PassesInnerExceptionToProducedException()
        {
            var inner = new InvalidOperationException(fuzzy.String());
            ServiceException serviceException = ServiceExceptionFor(typeof(DuplicateMessageException), message);

            bool result = sut.TryConvertFromServiceException(serviceException, inner, out Exception actual);

            Assert.True(result);
            Assert.IsType<DuplicateMessageException>(actual);
            Assert.Equal(message, actual.Message);
            Assert.Same(inner, actual.InnerException);
        }
    }

    public sealed class TryConvertFromServiceException_ServiceException_ExceptionArray : FabricActorExceptionConvertorTest
    {
        readonly string message = fuzzy.String();

        [Theory]
        [InlineData(typeof(DuplicateMessageException))]
        [InlineData(typeof(InvalidReentrantCallException))]
        [InlineData(typeof(ReminderNotFoundException))]
        [InlineData(typeof(ReentrancyModeDisallowedException))]
        [InlineData(typeof(ReentrantActorInvalidStateException))]
        [InlineData(typeof(ActorConcurrencyLockTimeoutException))]
        [InlineData(typeof(ActorDeletedException))]
        [InlineData(typeof(ReminderLoadInProgressException))]
        public void ReturnsTrueAndProducesKnownFabricExceptionWithPreservedMessage(Type knownType)
        {
            ServiceException serviceException = ServiceExceptionFor(knownType, message);

            bool result = sut.TryConvertFromServiceException(serviceException, (Exception[])null, out Exception actual);

            Assert.True(result);
            Assert.IsType(knownType, actual);
            Assert.Equal(message, actual.Message);
        }

        [Fact]
        public void ReturnsFalseWhenActualExceptionTypeIsUnknown()
        {
            var serviceException = new ServiceException(fuzzy.String(), message);

            bool result = sut.TryConvertFromServiceException(serviceException, (Exception[])null, out Exception actual);

            Assert.False(result);
            Assert.Null(actual);
        }
    }

    static ServiceException ServiceExceptionFor(Type knownType, string message) =>
        new(knownType.FullName, message)
        {
            ActualExceptionData = new Dictionary<string, string> { { "HResult", fuzzy.Int32().ToString() } },
        };
}
