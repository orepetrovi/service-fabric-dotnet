// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Inspector;
using Microsoft.ServiceFabric.Services.Client;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.Client;

// GetClientAsync(Uri, ...) and GetClientAsync(ResolvedServicePartition, ...) are not covered here.
// Both overloads always go through CreateClientWithRetriesAsync, which calls IServicePartitionResolver.ResolveAsync
// and then walks ResolvedServicePartition.Endpoints / GetEndpoint(). ResolvedServicePartition and
// ResolvedServiceEndpoint are sealed types from System.Fabric whose Endpoints collection cannot be populated
// without modifying the SUT to take a seam for endpoint selection. Verifying this code path is therefore
// considered an integration concern and is out of scope for these unit tests.
public abstract class CommunicationClientFactoryBaseTest : IDisposable
{
    readonly TestFactory sut;

    // Constructor parameters
    readonly bool fireConnectEvents = false;
    readonly IServicePartitionResolver servicePartitionResolver = Mock.Of<IServicePartitionResolver>();
    readonly IEnumerable<IExceptionHandler> exceptionHandlers = new[] { Mock.Of<IExceptionHandler>(), Mock.Of<IExceptionHandler>() };
    readonly string traceId = fuzzy.String();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    CommunicationClientFactoryBaseTest() =>
        sut = new TestFactory(fireConnectEvents, servicePartitionResolver, exceptionHandlers, traceId);

    void IDisposable.Dispose() =>
        sut.Dispose();

    public sealed class Constructor : CommunicationClientFactoryBaseTest
    {
        [Fact]
        public void InitializesProperties()
        {
            Assert.Same(servicePartitionResolver, sut.ServiceResolver);
            Assert.Equal(exceptionHandlers, sut.ExceptionHandlers);
            Assert.Equal(traceId, sut.TraceIdValue);
        }

        [Fact]
        public void UsesDefaultServicePartitionResolverWhenArgumentIsNull()
        {
            var other = new TestFactory(fireConnectEvents, null, exceptionHandlers, traceId);
            try { Assert.Same(ServicePartitionResolver.GetDefault(), other.ServiceResolver); }
            finally { other.Dispose(); }
        }

        [Fact]
        public void ExposesEmptyExceptionHandlersWhenArgumentIsNull()
        {
            var other = new TestFactory(fireConnectEvents, servicePartitionResolver, null, traceId);
            try { Assert.Empty(other.ExceptionHandlers); }
            finally { other.Dispose(); }
        }

        [Fact]
        public void GeneratesGuidTraceIdWhenArgumentIsNull()
        {
            var other = new TestFactory(fireConnectEvents, servicePartitionResolver, exceptionHandlers, null);
            try { Assert.True(Guid.TryParse(other.TraceIdValue, out _)); }
            finally { other.Dispose(); }
        }

        [Fact]
        public void CopiesExceptionHandlersToAvoidExternalMutation()
        {
            var handlers = new List<IExceptionHandler> { Mock.Of<IExceptionHandler>() };
            var other = new TestFactory(fireConnectEvents, servicePartitionResolver, handlers, traceId);
            try
            {
                handlers.Add(Mock.Of<IExceptionHandler>());
                Assert.Single(other.ExceptionHandlers);
            }
            finally { other.Dispose(); }
        }
    }

    public sealed class Dispose : CommunicationClientFactoryBaseTest
    {
        [Fact]
        public void DisposesClientCache()
        {
            var cache = sut.Field<CommunicationClientCache<ICommunicationClient>>().Value;
            Guid partitionId = fuzzy.Guid();
            ResolvedServiceEndpoint endpoint = MakeEndpoint(fuzzy.String());
            string listenerName = fuzzy.String();
            _ = cache.GetOrAddClientCacheEntry(partitionId, endpoint, listenerName, MakeRsp());

            sut.Dispose();

            Assert.False(cache.TryGetClientCacheEntry(partitionId, endpoint, listenerName, out _));
        }

        [Fact]
        public void IsIdempotent()
        {
            sut.Dispose();
            sut.Dispose();
        }
    }

    public sealed class OnClientConnected : CommunicationClientFactoryBaseTest
    {
        // Method parameters
        readonly ICommunicationClient newClient = Mock.Of<ICommunicationClient>();

        [Fact]
        public void FiresClientConnectedEventWithGivenClient()
        {
            object actualSender = null;
            CommunicationClientEventArgs<ICommunicationClient> actualArgs = null;
            sut.ClientConnected += (s, e) => { actualSender = s; actualArgs = e; };

            sut.OnClientConnected(newClient);

            Assert.Same(sut, actualSender);
            Assert.NotNull(actualArgs);
            Assert.Same(newClient, actualArgs.Client);
        }

        [Fact]
        public void DoesNotThrowWhenClientConnectedHasNoSubscribers() =>
            sut.OnClientConnected(newClient);
    }

    public sealed class OnClientDisconnected : CommunicationClientFactoryBaseTest
    {
        // Method parameters
        readonly ICommunicationClient faultedClient = Mock.Of<ICommunicationClient>();

        [Fact]
        public void FiresClientDisconnectedEventWithGivenClient()
        {
            object actualSender = null;
            CommunicationClientEventArgs<ICommunicationClient> actualArgs = null;
            sut.ClientDisconnected += (s, e) => { actualSender = s; actualArgs = e; };

            sut.OnClientDisconnected(faultedClient);

            Assert.Same(sut, actualSender);
            Assert.NotNull(actualArgs);
            Assert.Same(faultedClient, actualArgs.Client);
        }

        [Fact]
        public void DoesNotThrowWhenClientDisconnectedHasNoSubscribers() =>
            sut.OnClientDisconnected(faultedClient);
    }

    public sealed class ReportOperationExceptionAsync : CommunicationClientFactoryBaseTest
    {
        // Method parameters
        readonly ICommunicationClient client;
        readonly ExceptionInformation exceptionInformation;
        readonly OperationRetrySettings retrySettings = new();
        readonly CancellationToken cancellationToken = default;

        readonly Mock<IExceptionHandler> handler;
        readonly Exception reportedException = new InvalidOperationException(fuzzy.String());
        readonly ResolvedServicePartition rsp = MakeRsp();
        readonly ResolvedServiceEndpoint endpoint = MakeEndpoint(fuzzy.String());
        readonly string listenerName = fuzzy.String();

        public ReportOperationExceptionAsync()
        {
            client = Mock.Of<ICommunicationClient>(c =>
                c.ResolvedServicePartition == rsp &&
                c.Endpoint == endpoint &&
                c.ListenerName == listenerName);
            exceptionInformation = new ExceptionInformation(reportedException);
            handler = Mock.Get(exceptionHandlers.First());
        }

        [Fact]
        public async Task ReturnsShouldRetryFalseAndOriginalExceptionWhenNoHandlerMatches()
        {
            OperationRetryControl actual = await sut.ReportOperationExceptionAsync(
                client, exceptionInformation, retrySettings, cancellationToken);

            Assert.False(actual.ShouldRetry);
            Assert.Same(reportedException, actual.Exception);
            Assert.Equal(Timeout.InfiniteTimeSpan, actual.RetryDelay);
        }

        [Fact]
        public async Task ReturnsThrowResultExceptionToThrowWhenHandlerReturnsThrowResultWithReplacement()
        {
            var replacement = new ApplicationException();
            ExceptionHandlingResult result = new ExceptionHandlingThrowResult { ExceptionToThrow = replacement };
            _ = handler.Setup(_ => _.TryHandleException(exceptionInformation, retrySettings, out result)).Returns(true);

            OperationRetryControl actual = await sut.ReportOperationExceptionAsync(
                client, exceptionInformation, retrySettings, cancellationToken);

            Assert.False(actual.ShouldRetry);
            Assert.Same(replacement, actual.Exception);
        }

        [Fact]
        public async Task ReturnsOriginalExceptionWhenHandlerReturnsThrowResultWithoutReplacement()
        {
            ExceptionHandlingResult result = new ExceptionHandlingThrowResult();
            _ = handler.Setup(_ => _.TryHandleException(exceptionInformation, retrySettings, out result)).Returns(true);

            OperationRetryControl actual = await sut.ReportOperationExceptionAsync(
                client, exceptionInformation, retrySettings, cancellationToken);

            Assert.False(actual.ShouldRetry);
            Assert.Same(reportedException, actual.Exception);
        }

        [Fact]
        public async Task ReturnsRetryControlPopulatedFromTransientRetryResult()
        {
            var retry = new ExceptionHandlingRetryResult(
                reportedException, true, fuzzy.TimeSpan(), fuzzy.Int32());
            ExceptionHandlingResult result = retry;
            _ = handler.Setup(_ => _.TryHandleException(exceptionInformation, retrySettings, out result)).Returns(true);

            OperationRetryControl actual = await sut.ReportOperationExceptionAsync(
                client, exceptionInformation, retrySettings, cancellationToken);

            Assert.True(actual.ShouldRetry);
            Assert.True(actual.IsTransient);
            Assert.Equal(retry.RetryDelay, actual.RetryDelay);
            Assert.Equal(retry.ExceptionId, actual.ExceptionId);
            Assert.Equal(retry.MaxRetryCount, actual.MaxRetryCount);
            Assert.NotNull(actual.GetRetryDelay);
            Assert.Equal(retry.GetRetryDelay(0), actual.GetRetryDelay(0));
            Assert.Null(actual.Exception);
            Assert.Null(sut.AbortedClient);
        }

        [Fact]
        public async Task DoesNotAbortClientWhenNonTransientRetryAndCacheEntryHasNoClient()
        {
            var retry = new ExceptionHandlingRetryResult(
                reportedException, false, fuzzy.TimeSpan(), fuzzy.Int32());
            ExceptionHandlingResult result = retry;
            _ = handler.Setup(_ => _.TryHandleException(exceptionInformation, retrySettings, out result)).Returns(true);

            OperationRetryControl actual = await sut.ReportOperationExceptionAsync(
                client, exceptionInformation, retrySettings, cancellationToken);

            Assert.True(actual.ShouldRetry);
            Assert.False(actual.IsTransient);
            Assert.Null(sut.AbortedClient);
        }

        [Fact]
        public async Task DoesNotAbortClientWhenNonTransientRetryAndCacheEntryHasDifferentClient()
        {
            var cache = sut.Field<CommunicationClientCache<ICommunicationClient>>().Value;
            CommunicationClientCacheEntry<ICommunicationClient> entry =
                cache.GetOrAddClientCacheEntry(rsp.Info.Id, endpoint, listenerName, rsp);
            var cachedClient = Mock.Of<ICommunicationClient>();
            entry.Client = cachedClient;

            var retry = new ExceptionHandlingRetryResult(
                reportedException, false, fuzzy.TimeSpan(), fuzzy.Int32());
            ExceptionHandlingResult result = retry;
            _ = handler.Setup(_ => _.TryHandleException(exceptionInformation, retrySettings, out result)).Returns(true);

            OperationRetryControl actual = await sut.ReportOperationExceptionAsync(
                client, exceptionInformation, retrySettings, cancellationToken);

            Assert.True(actual.ShouldRetry);
            Assert.False(actual.IsTransient);
            Assert.Null(sut.AbortedClient);
            Assert.Same(cachedClient, entry.Client);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task AbortsClientAndClearsCacheEntryWhenNonTransientRetryAndClientMatchesCacheEntry(bool fireConnectEvents)
        {
            var sut = new TestFactory(fireConnectEvents, servicePartitionResolver, exceptionHandlers, traceId);
            try { await AbortsClientAndClearsCacheEntryWhenNonTransientRetryAndClientMatchesCacheEntryBody(sut, fireConnectEvents); }
            finally { sut.Dispose(); }
        }

        async Task AbortsClientAndClearsCacheEntryWhenNonTransientRetryAndClientMatchesCacheEntryBody(TestFactory sut, bool fireConnectEvents)
        {
            var cache = sut.Field<CommunicationClientCache<ICommunicationClient>>().Value;
            CommunicationClientCacheEntry<ICommunicationClient> entry =
                cache.GetOrAddClientCacheEntry(rsp.Info.Id, endpoint, listenerName, rsp);
            entry.Client = client;

            var disconnected = new List<ICommunicationClient>();
            sut.ClientDisconnected += (_, e) => disconnected.Add(e.Client);

            var retry = new ExceptionHandlingRetryResult(
                reportedException, false, fuzzy.TimeSpan(), fuzzy.Int32());
            ExceptionHandlingResult result = retry;
            _ = handler.Setup(_ => _.TryHandleException(exceptionInformation, retrySettings, out result)).Returns(true);

            _ = await sut.ReportOperationExceptionAsync(client, exceptionInformation, retrySettings, cancellationToken);

            Assert.Same(client, sut.AbortedClient);
            Assert.Null(entry.Client);
            Assert.Null(entry.Rsp);
            Assert.Equal(fireConnectEvents ? new[] { client } : Array.Empty<ICommunicationClient>(), disconnected);
        }

        [Fact]
        public async Task UnwrapsAggregateExceptionAndForwardsInnerExceptionsToHandler()
        {
            var inner = new InvalidOperationException(fuzzy.String());
            var info = new ExceptionInformation(new AggregateException(inner));

            ExceptionHandlingResult result = new ExceptionHandlingRetryResult(inner, true, fuzzy.TimeSpan(), 1);
            _ = handler
                .Setup(_ => _.TryHandleException(
                    It.Is<ExceptionInformation>(ei => ei.Exception == inner && ei.TargetReplica == info.TargetReplica),
                    retrySettings,
                    out result))
                .Returns(true);

            OperationRetryControl actual = await sut.ReportOperationExceptionAsync(client, info, retrySettings, cancellationToken);

            Assert.True(actual.ShouldRetry);
        }

        [Fact]
        public async Task InvokesSubsequentHandlerWhenPreviousHandlerDoesNotMatch()
        {
            var handler2 = Mock.Get(exceptionHandlers.Last());
            var retry = new ExceptionHandlingRetryResult(
                reportedException, true, fuzzy.TimeSpan(), fuzzy.Int32().Minimum(1));
            ExceptionHandlingResult result = retry;
            _ = handler2.Setup(_ => _.TryHandleException(exceptionInformation, retrySettings, out result)).Returns(true);

            OperationRetryControl actual = await sut.ReportOperationExceptionAsync(
                client, exceptionInformation, retrySettings, cancellationToken);

            Assert.True(actual.ShouldRetry);
            handler.Verify(
                _ => _.TryHandleException(exceptionInformation, retrySettings, out It.Ref<ExceptionHandlingResult>.IsAny),
                Times.Once);
        }

        [Fact]
        public async Task DoesNotInvokeSubsequentHandlerWhenPreviousHandlerMatches()
        {
            ExceptionHandlingResult result = new ExceptionHandlingThrowResult();
            _ = handler.Setup(_ => _.TryHandleException(exceptionInformation, retrySettings, out result)).Returns(true);
            var handler2 = Mock.Get(exceptionHandlers.Last());

            _ = await sut.ReportOperationExceptionAsync(
                client, exceptionInformation, retrySettings, cancellationToken);

            handler2.Verify(
                _ => _.TryHandleException(It.IsAny<ExceptionInformation>(), It.IsAny<OperationRetrySettings>(), out It.Ref<ExceptionHandlingResult>.IsAny),
                Times.Never);
        }
    }

    static ResolvedServiceEndpoint MakeEndpoint(string address)
    {
        var endpoint = new ResolvedServiceEndpoint();
        endpoint.Property<string>().Set(address);
        return endpoint;
    }

    static ResolvedServicePartition MakeRsp()
    {
        var rsp = Type<ResolvedServicePartition>.Uninitialized();
        rsp.Property<ServicePartitionInformation>().Set(Type<SingletonPartitionInformation>.Uninitialized());
        return rsp;
    }

    sealed class TestFactory : CommunicationClientFactoryBase<ICommunicationClient>
    {
        internal ICommunicationClient AbortedClient;

        internal TestFactory(
            bool fireConnectEvents,
            IServicePartitionResolver servicePartitionResolver,
            IEnumerable<IExceptionHandler> exceptionHandlers,
            string traceId)
            : base(fireConnectEvents, servicePartitionResolver, exceptionHandlers, traceId)
        {
        }

        internal string TraceIdValue => TraceId;

        protected override bool ValidateClient(ICommunicationClient client) => true;

        protected override bool ValidateClient(string endpoint, ICommunicationClient client) => true;

        protected override Task<ICommunicationClient> CreateClientAsync(string endpoint, CancellationToken cancellationToken) =>
            Task.FromResult(Mock.Of<ICommunicationClient>());

        protected override void AbortClient(ICommunicationClient client) =>
            AbortedClient = client;
    }
}
