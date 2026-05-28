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
        readonly ServiceException serviceException;

        public TryConvertFromServiceException_ServiceException() =>
            serviceException = ServiceExceptionFor(typeof(DuplicateMessageException), message);

        [Fact]
        public void ProducesKnownFabricExceptionWithoutInnerException()
        {
            bool result = sut.TryConvertFromServiceException(serviceException, out Exception actual);

            Assert.True(result);
            Assert.IsType<DuplicateMessageException>(actual);
            Assert.Equal(message, actual.Message);
            Assert.Null(actual.InnerException);
        }

        [Fact]
        public void ReturnsFalseWhenActualExceptionTypeIsUnknown()
        {
            var serviceException = new ServiceException(fuzzy.String(), message);

            bool result = sut.TryConvertFromServiceException(serviceException, out Exception actual);

            Assert.False(result);
            Assert.Null(actual);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. TryConvertFromServiceException doesn't validate serviceException.
        public void ThrowsArgumentNullExceptionWhenServiceExceptionIsNull()
        {
            // TryConvertFromServiceException dereferences serviceException.ActualExceptionType without validation,
            // surfacing the defect as a NullReferenceException.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.TryConvertFromServiceException(null, out Exception _));
            Assert.Equal(nameof(serviceException), exception.ParamName);
        }
    }

    public sealed class TryConvertFromServiceException_ServiceException_Exception : FabricActorExceptionConvertorTest
    {
        readonly string message = fuzzy.String();
        readonly Exception innerException = new InvalidOperationException(fuzzy.String());
        readonly ServiceException serviceException;

        public TryConvertFromServiceException_ServiceException_Exception() =>
            serviceException = ServiceExceptionFor(typeof(DuplicateMessageException), message);

        [Fact]
        public void PassesInnerExceptionToProducedException()
        {
            bool result = sut.TryConvertFromServiceException(serviceException, innerException, out Exception actual);

            Assert.True(result);
            Assert.IsType<DuplicateMessageException>(actual);
            Assert.Equal(message, actual.Message);
            Assert.Same(innerException, actual.InnerException);
        }

        [Fact]
        public void ReturnsFalseWhenActualExceptionTypeIsUnknown()
        {
            var serviceException = new ServiceException(fuzzy.String(), message);

            bool result = sut.TryConvertFromServiceException(serviceException, innerException, out Exception actual);

            Assert.False(result);
            Assert.Null(actual);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. TryConvertFromServiceException doesn't validate serviceException.
        public void ThrowsArgumentNullExceptionWhenServiceExceptionIsNull()
        {
            // TryConvertFromServiceException dereferences serviceException.ActualExceptionType without validation,
            // surfacing the defect as a NullReferenceException.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.TryConvertFromServiceException(null, innerException, out Exception _));
            Assert.Equal(nameof(serviceException), exception.ParamName);
        }
    }

    public sealed class TryConvertFromServiceException_ServiceException_ExceptionArray : FabricActorExceptionConvertorTest
    {
        readonly string message = fuzzy.String();
        readonly Exception[] innerExceptions = null;

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

            bool result = sut.TryConvertFromServiceException(serviceException, innerExceptions, out Exception actual);

            Assert.True(result);
            Assert.IsType(knownType, actual);
            Assert.Equal(message, actual.Message);
            Assert.Null(actual.InnerException);
        }

        [Fact]
        public void ReturnsFalseWhenActualExceptionTypeIsUnknown()
        {
            var serviceException = new ServiceException(fuzzy.String(), message);

            bool result = sut.TryConvertFromServiceException(serviceException, innerExceptions, out Exception actual);

            Assert.False(result);
            Assert.Null(actual);
        }

        [Fact]
        public void PassesFirstInnerExceptionToProducedException()
        {
            var innerException = new InvalidOperationException(fuzzy.String());
            ServiceException serviceException = ServiceExceptionFor(typeof(DuplicateMessageException), message);

            bool result = sut.TryConvertFromServiceException(serviceException, new[] { innerException, new Exception(fuzzy.String()) }, out Exception actual);

            Assert.True(result);
            Assert.IsType<DuplicateMessageException>(actual);
            Assert.Equal(message, actual.Message);
            Assert.Same(innerException, actual.InnerException);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. TryConvertFromServiceException doesn't validate serviceException.
        public void ThrowsArgumentNullExceptionWhenServiceExceptionIsNull()
        {
            // TryConvertFromServiceException dereferences serviceException.ActualExceptionType without validation,
            // surfacing the defect as a NullReferenceException.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.TryConvertFromServiceException(null, innerExceptions, out Exception _));
            Assert.Equal("serviceException", exception.ParamName);
        }
    }

    static ServiceException ServiceExceptionFor(Type knownType, string message) =>
        new(knownType.FullName, message)
        {
            ActualExceptionData = new Dictionary<string, string> { { nameof(Exception.HResult), fuzzy.Int32().ToString() } },
        };
}
