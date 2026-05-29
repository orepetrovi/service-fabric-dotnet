// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Inspector;
using Microsoft.ServiceFabric.Services.Client;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.Client;

public abstract class ServicePartitionClientTest
{
    readonly ServicePartitionClient<ICommunicationClient> sut;

    // Constructor parameters
    readonly Mock<ICommunicationClientFactory<ICommunicationClient>> communicationClientFactory = new(MockBehavior.Strict);
    readonly Uri serviceUri = fuzzy.Uri();
    readonly ServicePartitionKey partitionKey = new(fuzzy.Int64());
    readonly TargetReplicaSelector targetReplicaSelector = fuzzy.Enum<TargetReplicaSelector>();
    readonly string listenerName = fuzzy.String();
    readonly OperationRetrySettings retrySettings = new();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ServicePartitionClientTest() =>
        sut = new ServicePartitionClient<ICommunicationClient>(
            communicationClientFactory.Object,
            serviceUri,
            partitionKey,
            targetReplicaSelector,
            listenerName,
            retrySettings);

    public sealed class Constructor : ServicePartitionClientTest
    {
        [Fact]
        public void InitializesProperties()
        {
            Assert.Same(communicationClientFactory.Object, sut.Factory);
            Assert.Same(serviceUri, sut.ServiceUri);
            Assert.Same(partitionKey, sut.PartitionKey);
            Assert.Same(listenerName, sut.ListenerName);
        }

        [Theory]
        [InlineData(TargetReplicaSelector.Default)]
        [InlineData(TargetReplicaSelector.RandomReplica)]
        [InlineData(TargetReplicaSelector.RandomSecondaryReplica)]
        public void InitializesTargetReplicaSelector(TargetReplicaSelector selector)
        {
            var sut = new ServicePartitionClient<ICommunicationClient>(
                communicationClientFactory.Object, serviceUri, partitionKey, selector, listenerName, retrySettings);
            Assert.Equal(selector, sut.TargetReplicaSelector);
        }

        [Fact]
        public void DefaultsPartitionKeyToSingletonWhenPartitionKeyIsNull()
        {
            var sut = new ServicePartitionClient<ICommunicationClient>(
                communicationClientFactory.Object, serviceUri, null, targetReplicaSelector, listenerName, retrySettings);
            Assert.Same(ServicePartitionKey.Singleton, sut.PartitionKey);
        }

        [Fact]
        public async Task DefaultsRetrySettingsWhenArgumentIsNull()
        {
            var sut = new ServicePartitionClient<ICommunicationClient>(
                communicationClientFactory.Object, serviceUri, partitionKey, targetReplicaSelector, listenerName, null);

            // Capture the retrySettings forwarded to GetClientAsync and assert observable defaults.
            OperationRetrySettings forwarded = null;
            var communicationClient = new Mock<ICommunicationClient>();
            _ = communicationClientFactory
                .Setup(_ => _.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, It.IsAny<OperationRetrySettings>(), It.IsAny<CancellationToken>()))
                .Callback((Uri _, ServicePartitionKey _, TargetReplicaSelector _, string _, OperationRetrySettings settings, CancellationToken _) => forwarded = settings)
                .ReturnsAsync(communicationClient.Object);

            _ = await sut.InvokeWithRetryAsync<object>(_ => Task.FromResult(new object()), TestContext.Current.CancellationToken);

            Assert.NotNull(forwarded);
        }

        [Fact]
        public void DefaultsTargetReplicaSelectorToDefault()
        {
            var sut = new ServicePartitionClient<ICommunicationClient>(communicationClientFactory.Object, serviceUri);
            Assert.Equal(TargetReplicaSelector.Default, sut.TargetReplicaSelector);
        }

        [Fact]
        public void DefaultsListenerNameToNull()
        {
            var sut = new ServicePartitionClient<ICommunicationClient>(communicationClientFactory.Object, serviceUri);
            Assert.Null(sut.ListenerName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation for communicationClientFactory.
        public void ThrowsArgumentNullExceptionWhenCommunicationClientFactoryIsNull()
        {
            var actual = Assert.Throws<ArgumentNullException>(
                () => new ServicePartitionClient<ICommunicationClient>(
                    null, serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings));
            Assert.Equal(nameof(communicationClientFactory), actual.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation for serviceUri.
        public void ThrowsArgumentNullExceptionWhenServiceUriIsNull()
        {
            var actual = Assert.Throws<ArgumentNullException>(
                () => new ServicePartitionClient<ICommunicationClient>(
                    communicationClientFactory.Object, null, partitionKey, targetReplicaSelector, listenerName, retrySettings));
            Assert.Equal(nameof(serviceUri), actual.ParamName);
        }
    }

    public sealed class InvokeWithRetryAsync_FuncOfICommunicationClientTaskOfObject_CancellationToken_TypeArray : InvokeWithRetryAsyncBase
    {
        static readonly Method method = typeof(ServicePartitionClient<ICommunicationClient>)
            .Method<Func<Func<ICommunicationClient, Task<object>>, CancellationToken, Type[], Task<object>>>(
                nameof(ServicePartitionClient<ICommunicationClient>.InvokeWithRetryAsync));

        readonly Func<ICommunicationClient, Task<object>> func = _ => Task.FromResult(new object());
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        readonly Type[] doNotRetryExceptionTypes = Type.EmptyTypes;

        [Fact]
        public async Task ReturnsResultOfFuncWhenNoExceptionThrown()
        {
            var expected = new object();

            object actual = await sut.InvokeWithRetryAsync<object>(_ => Task.FromResult(expected), cancellationToken);

            Assert.Same(expected, actual);
        }

        [Fact]
        public async Task PassesResolvedClientToFunc()
        {
            ICommunicationClient actual = null;

            _ = await sut.InvokeWithRetryAsync<object>(c => { actual = c; return Task.FromResult(new object()); }, cancellationToken);

            Assert.Same(client, actual);
        }

        [Fact]
        public async Task PassesCancellationTokenToGetClientAsync()
        {
            _ = await sut.InvokeWithRetryAsync<object>(_ => Task.FromResult(new object()), cancellationToken);

            communicationClientFactory.Verify(
                _ => _.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, cancellationToken),
                Times.Once);
        }

        [Fact]
        public async Task RetriesAfterTransientException()
        {
            SetupReportOperationException(TransientRetry());
            int calls = 0;

            object actual = await sut.InvokeWithRetryAsync<object>(
                _ =>
                {
                    calls++;
                    if (calls == 1) throw clientException;
                    return Task.FromResult<object>(calls);
                },
                cancellationToken);

            Assert.Equal(2, calls);
            Assert.Equal(2, actual);
        }

        [Fact]
        public async Task RethrowsExceptionWhenInDoNotRetryExceptionTypes()
        {
            Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.InvokeWithRetryAsync<object>(
                    _ => throw clientException,
                    cancellationToken,
                    clientException.GetType()));

            Assert.Same(clientException, actual);
        }

        [Fact]
        public async Task RetriesAfterAggregateExceptionWhenNoInnerExceptionIsInDoNotRetryExceptionTypes()
        {
            var aggregate = new AggregateException(clientException);
            SetupReportOperationException(TransientRetry(), aggregate);
            int calls = 0;

            object actual = await sut.InvokeWithRetryAsync<object>(
                _ =>
                {
                    calls++;
                    if (calls == 1) throw aggregate;
                    return Task.FromResult<object>(calls);
                },
                cancellationToken);

            Assert.Equal(2, calls);
            Assert.Equal(2, actual);
        }

        [Fact]
        public async Task RethrowsAggregateExceptionWhenInnerExceptionIsInDoNotRetryExceptionTypes()
        {
            var aggregate = new AggregateException(clientException);

            AggregateException actual = await Assert.ThrowsAsync<AggregateException>(
                () => sut.InvokeWithRetryAsync<object>(
                    _ => throw aggregate,
                    cancellationToken,
                    clientException.GetType()));

            Assert.Same(clientException, Assert.Single(actual.InnerExceptions));
        }

        [Fact]
        public async Task ThrowsExceptionFromReportResultWhenShouldRetryIsFalseAndReportExceptionIsSet()
        {
            var transformed = new ApplicationException();
            SetupReportOperationException(new OperationRetryControl { ShouldRetry = false, Exception = transformed });

            ApplicationException actual = await Assert.ThrowsAsync<ApplicationException>(
                () => sut.InvokeWithRetryAsync<object>(_ => throw clientException, cancellationToken));

            Assert.Same(transformed, actual);
        }

        [Fact]
        public async Task ThrowsOriginalExceptionWhenShouldRetryIsFalseAndReportExceptionIsNull()
        {
            SetupReportOperationException(new OperationRetryControl { ShouldRetry = false, Exception = null });

            Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.InvokeWithRetryAsync<object>(_ => throw clientException, cancellationToken));

            Assert.Same(clientException, actual);
        }

        [Fact]
        public async Task ThrowsAfterMaxRetryCountReached()
        {
            SetupReportOperationException(TransientRetry(maxRetryCount: 2));
            int calls = 0;

            Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.InvokeWithRetryAsync<object>(
                    _ => { calls++; throw clientException; },
                    cancellationToken));

            Assert.Same(clientException, actual);
            Assert.Equal(3, calls);
        }

        [Fact]
        public async Task ThrowsAfterZeroMaxRetryCount()
        {
            SetupReportOperationException(new OperationRetryControl { ShouldRetry = true, IsTransient = true, MaxRetryCount = 0, ExceptionId = "ex", GetRetryDelay = _ => ShortRetryDelay });
            int calls = 0;

            Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.InvokeWithRetryAsync<object>(
                    _ => { calls++; throw clientException; },
                    cancellationToken));

            Assert.Same(clientException, actual);
            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task ResetsRetryCountWhenExceptionIdChangesAcrossIterations()
        {
            // Utility.ShouldRetryOperation resets the retry counter whenever ExceptionId differs from the previous
            // iteration. Verify the SUT forwards ExceptionId so distinct ids cause more than MaxRetryCount retries.
            const int maxRetryCount = 2;
            var controls = new Queue<OperationRetryControl>(new[]
            {
                Retry("a"), // count: null→"a", 1
                Retry("a"), // count: 2
                Retry("b"), // reset: "a"→"b", 1
                Retry("b"), // count: 2
                Retry("b"), // 2 >= max → throws
            });
            _ = communicationClientFactory
                .Setup(_ => _.ReportOperationExceptionAsync(
                    client,
                    It.Is<ExceptionInformation>(i => i.Exception == clientException && i.TargetReplica == targetReplicaSelector),
                    retrySettings,
                    CancellationToken.None))
                .ReturnsAsync(controls.Dequeue);
            int calls = 0;

            Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.InvokeWithRetryAsync<object>(
                    _ => { calls++; throw clientException; },
                    cancellationToken));

            Assert.Same(clientException, actual);
            Assert.Equal(5, calls);

            static OperationRetryControl Retry(string id) => new()
            {
                ShouldRetry = true,
                IsTransient = true,
                MaxRetryCount = maxRetryCount,
                ExceptionId = id,
                GetRetryDelay = _ => ShortRetryDelay,
            };
        }

        [Fact]
        public async Task ResetsCommunicationClientWhenExceptionIsNotTransient()
        {
            // After a non-transient exception, the SUT clears the cached client. Because lastRsp has already been
            // captured from the first GetClientAsync call, the next iteration takes the rsp-based GetClientAsync
            // overload.
            SetupReportOperationException(NonTransientRetry());
            int calls = 0;

            _ = await sut.InvokeWithRetryAsync<object>(
                _ =>
                {
                    calls++;
                    if (calls == 1) throw clientException;
                    return Task.FromResult<object>(calls);
                },
                cancellationToken);

            communicationClientFactory.Verify(
                _ => _.GetClientAsync(rsp, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()),
                Times.Once);
            communicationClientFactory.Verify(
                _ => _.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdatesLastRspFromReResolvedClientAfterNonTransientException()
        {
            // After a non-transient exception the SUT re-resolves via the rsp-based GetClientAsync overload. The
            // second client carries a distinct ResolvedServicePartition, and the SUT must adopt it as the new lastRsp.
            var newRsp = Type<ResolvedServicePartition>.Uninitialized();
            var newCommunicationClient = new Mock<ICommunicationClient>();
            _ = newCommunicationClient.SetupGet(_ => _.ResolvedServicePartition).Returns(newRsp);
            _ = communicationClientFactory
                .Setup(_ => _.GetClientAsync(rsp, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()))
                .ReturnsAsync(newCommunicationClient.Object);
            SetupReportOperationException(NonTransientRetry());
            int calls = 0;

            _ = await sut.InvokeWithRetryAsync<object>(
                _ =>
                {
                    calls++;
                    if (calls == 1) throw clientException;
                    return Task.FromResult<object>(calls);
                },
                cancellationToken);

            Assert.True(sut.TryGetLastResolvedServicePartition(out ResolvedServicePartition actual));
            Assert.Same(newRsp, actual);
        }

        [Fact]
        public async Task DoesNotResetCommunicationClientWhenExceptionIsTransient()
        {
            SetupReportOperationException(TransientRetry());
            int calls = 0;

            _ = await sut.InvokeWithRetryAsync<object>(
                _ =>
                {
                    calls++;
                    if (calls == 1) throw clientException;
                    return Task.FromResult<object>(calls);
                },
                cancellationToken);

            communicationClientFactory.Verify(
                _ => _.GetClientAsync(rsp, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()),
                Times.Never);
            communicationClientFactory.Verify(
                _ => _.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ThrowsOperationCanceledExceptionWhenTokenIsCanceledBeforeFunc()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.InvokeWithRetryAsync<object>(_ => Task.FromResult(new object()), cts.Token));

            communicationClientFactory.Verify(
                _ => _.GetClientAsync(rsp, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()),
                Times.Never);
            communicationClientFactory.Verify(
                _ => _.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CancelsWhenTokenIsSignaledMidRetry()
        {
            SetupReportOperationException(TransientRetry());
            using var cts = new CancellationTokenSource();
            int calls = 0;
            int retryCount = 5;

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.InvokeWithRetryAsync<object>(
                    _ =>
                    {
                        calls++;
                        if (calls == retryCount) cts.Cancel();
                        throw clientException;
                    },
                    cts.Token));

            Assert.Equal(retryCount, calls);
        }

        [Fact]
        public async Task CancelsWhenClientRetryTimeoutElapses()
        {
            var timeout = TimeSpan.FromMilliseconds(500);
            var policy = new Mock<IRetryPolicy>();
            _ = policy.SetupGet(_ => _.ClientRetryTimeout).Returns(timeout);
            var retrySettings = new OperationRetrySettings(policy.Object);
            var sut = new ServicePartitionClient<ICommunicationClient>(
                communicationClientFactory.Object,
                serviceUri,
                partitionKey,
                targetReplicaSelector,
                listenerName,
                retrySettings);
            _ = communicationClientFactory
                .Setup(_ => _.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()))
                .ReturnsAsync(client);
            _ = communicationClientFactory
                .Setup(_ => _.ReportOperationExceptionAsync(
                    client,
                    It.Is<ExceptionInformation>(i => i.Exception == clientException && i.TargetReplica == targetReplicaSelector),
                    retrySettings,
                    CancellationToken.None))
                .ReturnsAsync(TransientRetry(maxRetryCount: 1_000_000, delay: ShortRetryDelay));
            using var cts = new CancellationTokenSource();
            int calls = 0;

            var stopwatch = Stopwatch.StartNew();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.InvokeWithRetryAsync<object>(
                    _ => { calls++; throw clientException; },
                    cts.Token));
            stopwatch.Stop();

            // Stopwatch precision varies; allow a small margin per Microsoft documentation.
            Assert.True(stopwatch.ElapsedMilliseconds >= timeout.TotalMilliseconds - 25, $"Elapsed {stopwatch.ElapsedMilliseconds}ms < {timeout.TotalMilliseconds}ms");
            Assert.False(cts.IsCancellationRequested);
            communicationClientFactory.Verify(
                _ => _.ReportOperationExceptionAsync(
                    client,
                    It.Is<ExceptionInformation>(i => i.Exception == clientException && i.TargetReplica == targetReplicaSelector),
                    retrySettings,
                    CancellationToken.None),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task CancelsBeforeLargeRetryDelayElapses()
        {
            var timeout = TimeSpan.FromMilliseconds(100);
            var retryDelay = TimeSpan.FromSeconds(30);
            var policy = new Mock<IRetryPolicy>();
            _ = policy.SetupGet(_ => _.ClientRetryTimeout).Returns(timeout);
            var retrySettings = new OperationRetrySettings(policy.Object);
            var sut = new ServicePartitionClient<ICommunicationClient>(
                communicationClientFactory.Object,
                serviceUri,
                partitionKey,
                targetReplicaSelector,
                listenerName,
                retrySettings);
            _ = communicationClientFactory
                .Setup(_ => _.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()))
                .ReturnsAsync(client);
            _ = communicationClientFactory
                .Setup(_ => _.ReportOperationExceptionAsync(
                    client,
                    It.Is<ExceptionInformation>(i => i.Exception == clientException && i.TargetReplica == targetReplicaSelector),
                    retrySettings,
                    CancellationToken.None))
                .ReturnsAsync(TransientRetry(maxRetryCount: 1_000_000, delay: retryDelay));

            var stopwatch = Stopwatch.StartNew();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.InvokeWithRetryAsync<object>(_ => throw clientException, TestContext.Current.CancellationToken));
            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds < retryDelay.TotalMilliseconds, $"Elapsed {stopwatch.ElapsedMilliseconds}ms >= retry delay {retryDelay.TotalMilliseconds}ms");
            communicationClientFactory.Verify(
                _ => _.ReportOperationExceptionAsync(
                    client,
                    It.Is<ExceptionInformation>(i => i.Exception == clientException && i.TargetReplica == targetReplicaSelector),
                    retrySettings,
                    CancellationToken.None),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task SetsClientRequestTrackerToLogContextRequestIdWhenPresent()
        {
            LogContext.Clear();
            ClientRequestTracker.Set(null);
            try
            {
                var requestId = fuzzy.Guid();
                LogContext.Set(new LogContext { RequestId = requestId });
                string actual = null;

                _ = await sut.InvokeWithRetryAsync<object>(
                    _ =>
                    {
                        ClientRequestTracker.TryGet(out actual);
                        return Task.FromResult(new object());
                    },
                    cancellationToken);

                Assert.Equal(requestId.ToString(), actual);
            }
            finally
            {
                LogContext.Clear();
                ClientRequestTracker.Set(null);
            }
        }

        [Fact]
        public async Task SetsClientRequestTrackerToGeneratedGuidWhenLogContextIsAbsent()
        {
            LogContext.Clear();
            ClientRequestTracker.Set(null);
            try
            {
                string actual = null;

                _ = await sut.InvokeWithRetryAsync<object>(
                    _ =>
                    {
                        ClientRequestTracker.TryGet(out actual);
                        return Task.FromResult(new object());
                    },
                    cancellationToken);

                Assert.True(Guid.TryParse(actual, out Guid parsed));
                Assert.NotEqual(Guid.Empty, parsed);
            }
            finally
            {
                LogContext.Clear();
                ClientRequestTracker.Set(null);
            }
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation for func.
        public async Task ThrowsArgumentNullExceptionWhenFuncIsNull()
        {
            var actual = await Assert.ThrowsAsync<ArgumentNullException>(
                () => sut.InvokeWithRetryAsync<object>((Func<ICommunicationClient, Task<object>>)null, cancellationToken));
            Assert.Equal(method.Parameter<Func<ICommunicationClient, Task<object>>>().Name, actual.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation for doNotRetryExceptionTypes.
        public async Task ThrowsArgumentNullExceptionWhenDoNotRetryExceptionTypesIsNull()
        {
            var actual = await Assert.ThrowsAsync<ArgumentNullException>(
                () => sut.InvokeWithRetryAsync<object>(_ => throw clientException, cancellationToken, (Type[])null));
            Assert.Equal(method.Parameter<Type[]>().Name, actual.ParamName);
        }
    }

    public sealed class InvokeWithRetryAsync_FuncOfICommunicationClientTaskOfObject_TypeArray : InvokeWithRetryAsyncBase
    {
        static readonly Method method = typeof(ServicePartitionClient<ICommunicationClient>)
            .Method<Func<Func<ICommunicationClient, Task<object>>, Type[], Task<object>>>(
                nameof(ServicePartitionClient<ICommunicationClient>.InvokeWithRetryAsync));

        readonly Func<ICommunicationClient, Task<object>> func = _ => Task.FromResult(new object());
        readonly Type[] doNotRetryExceptionTypes = Type.EmptyTypes;

        [Fact]
        public async Task ReturnsResultOfFuncWhenNoExceptionThrown()
        {
            var expected = new object();

            object actual = await sut.InvokeWithRetryAsync<object>(_ => Task.FromResult(expected));

            Assert.Same(expected, actual);
        }

        [Fact]
        public async Task DefaultsCancellationTokenToNoneWhenNotSpecified()
        {
            _ = await sut.InvokeWithRetryAsync<object>(_ => Task.FromResult(new object()));

            communicationClientFactory.Verify(
                _ => _.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, CancellationToken.None),
                Times.Once);
        }

        [Fact]
        public async Task RethrowsExceptionWhenInDoNotRetryExceptionTypes()
        {
            Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.InvokeWithRetryAsync<object>(_ => throw clientException, clientException.GetType()));

            Assert.Same(clientException, actual);
        }

        [Fact]
        public async Task RetriesAfterTransientException()
        {
            // The no-token overload delegates to the core overload with CancellationToken.None. Verify the retry
            // loop is reachable through this delegation, not just the success and do-not-retry branches.
            SetupReportOperationException(TransientRetry());
            int calls = 0;

            object actual = await sut.InvokeWithRetryAsync<object>(
                _ =>
                {
                    calls++;
                    if (calls == 1) throw clientException;
                    return Task.FromResult<object>(calls);
                });

            Assert.Equal(2, calls);
            Assert.Equal(2, actual);
        }
    }

    public sealed class InvokeWithRetryAsync_FuncOfICommunicationClientTask_CancellationToken_TypeArray : InvokeWithRetryAsyncBase
    {
        static readonly Method method = typeof(ServicePartitionClient<ICommunicationClient>)
            .Method<Func<Func<ICommunicationClient, Task>, CancellationToken, Type[], Task>>(
                nameof(ServicePartitionClient<ICommunicationClient>.InvokeWithRetryAsync));

        readonly Func<ICommunicationClient, Task> func = _ => Task.CompletedTask;
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        readonly Type[] doNotRetryExceptionTypes = Type.EmptyTypes;

        [Fact]
        public async Task PassesResolvedClientToFunc()
        {
            ICommunicationClient actual = null;

            await sut.InvokeWithRetryAsync(c => { actual = c; return Task.CompletedTask; }, cancellationToken);

            Assert.Same(client, actual);
        }

        [Fact]
        public async Task PassesCancellationTokenToGetClientAsync()
        {
            await sut.InvokeWithRetryAsync(_ => Task.CompletedTask, cancellationToken);

            communicationClientFactory.Verify(
                _ => _.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, cancellationToken),
                Times.Once);
        }

        [Fact]
        public async Task RethrowsExceptionWhenInDoNotRetryExceptionTypes()
        {
            Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.InvokeWithRetryAsync(
                    _ => throw clientException,
                    cancellationToken,
                    clientException.GetType()));

            Assert.Same(clientException, actual);
        }

        [Fact]
        public async Task AwaitsTaskReturnedByFunc()
        {
            Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.InvokeWithRetryAsync(
                    _ => Task.FromException(clientException),
                    cancellationToken,
                    clientException.GetType()));

            Assert.Same(clientException, actual);
        }

        [Fact]
        public async Task RetriesAfterTransientException()
        {
            // The Task-returning overload wraps func into a synthetic Func<Task<object>>. Verify the wrapper is
            // re-invoked across retry iterations and observes both throw and success paths.
            SetupReportOperationException(TransientRetry());
            int calls = 0;

            await sut.InvokeWithRetryAsync(
                _ =>
                {
                    calls++;
                    if (calls == 1) throw clientException;
                    return Task.CompletedTask;
                },
                cancellationToken);

            Assert.Equal(2, calls);
        }

        [Fact]
        public async Task RethrowsAggregateExceptionWhenInnerExceptionIsInDoNotRetryExceptionTypes()
        {
            // Verify the wrapper does not interfere with the callee's AggregateException unwrap branch.
            var aggregate = new AggregateException(clientException);

            AggregateException actual = await Assert.ThrowsAsync<AggregateException>(
                () => sut.InvokeWithRetryAsync(
                    _ => throw aggregate,
                    cancellationToken,
                    clientException.GetType()));

            Assert.Same(clientException, Assert.Single(actual.InnerExceptions));
        }

        [Fact]
        public async Task ResetsCommunicationClientWhenExceptionIsNotTransient()
        {
            // Non-transient retries clear the cached client and re-resolve via the rsp-based GetClientAsync overload.
            // Verify the wrapper participates correctly across the reset and is re-invoked on the new client.
            SetupReportOperationException(NonTransientRetry());
            int calls = 0;

            await sut.InvokeWithRetryAsync(
                _ =>
                {
                    calls++;
                    if (calls == 1) throw clientException;
                    return Task.CompletedTask;
                },
                cancellationToken);

            communicationClientFactory.Verify(
                _ => _.GetClientAsync(rsp, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CancelsWhenTokenIsSignaledMidRetry()
        {
            // Verify the wrapper does not suppress cancellation when the caller's token fires between retries.
            SetupReportOperationException(TransientRetry());
            using var cts = new CancellationTokenSource();
            int calls = 0;
            int retryCount = 3;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.InvokeWithRetryAsync(
                    _ =>
                    {
                        calls++;
                        if (calls == retryCount) cts.Cancel();
                        throw clientException;
                    },
                    cts.Token));

            Assert.Equal(retryCount, calls);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation for func.
        public async Task ThrowsArgumentNullExceptionWhenFuncIsNull()
        {
            var actual = await Assert.ThrowsAsync<ArgumentNullException>(
                () => sut.InvokeWithRetryAsync((Func<ICommunicationClient, Task>)null, cancellationToken));
            Assert.Equal(method.Parameter<Func<ICommunicationClient, Task>>().Name, actual.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation for doNotRetryExceptionTypes.
        public async Task ThrowsArgumentNullExceptionWhenDoNotRetryExceptionTypesIsNull()
        {
            var actual = await Assert.ThrowsAsync<ArgumentNullException>(
                () => sut.InvokeWithRetryAsync(_ => throw clientException, cancellationToken, (Type[])null));
            Assert.Equal(method.Parameter<Type[]>().Name, actual.ParamName);
        }
    }

    public sealed class InvokeWithRetryAsync_FuncOfICommunicationClientTask_TypeArray : InvokeWithRetryAsyncBase
    {
        static readonly Method method = typeof(ServicePartitionClient<ICommunicationClient>)
            .Method<Func<Func<ICommunicationClient, Task>, Type[], Task>>(
                nameof(ServicePartitionClient<ICommunicationClient>.InvokeWithRetryAsync));

        readonly Func<ICommunicationClient, Task> func = _ => Task.CompletedTask;
        readonly Type[] doNotRetryExceptionTypes = Type.EmptyTypes;

        [Fact]
        public async Task PassesResolvedClientToFunc()
        {
            ICommunicationClient actual = null;

            await sut.InvokeWithRetryAsync(c => { actual = c; return Task.CompletedTask; });

            Assert.Same(client, actual);
        }

        [Fact]
        public async Task DefaultsCancellationTokenToNoneWhenNotSpecified()
        {
            await sut.InvokeWithRetryAsync(_ => Task.CompletedTask);

            communicationClientFactory.Verify(
                _ => _.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, CancellationToken.None),
                Times.Once);
        }

        [Fact]
        public async Task RethrowsExceptionWhenInDoNotRetryExceptionTypes()
        {
            Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.InvokeWithRetryAsync(_ => throw clientException, clientException.GetType()));

            Assert.Same(clientException, actual);
        }

        [Fact]
        public async Task AwaitsTaskReturnedByFunc()
        {
            Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.InvokeWithRetryAsync(_ => Task.FromException(clientException), clientException.GetType()));

            Assert.Same(clientException, actual);
        }

        [Fact]
        public async Task RetriesAfterTransientException()
        {
            // This overload composes the no-token delegation with the Func<Task> wrapping. Verify the retry loop
            // re-invokes the wrapper across iterations when both transformations apply.
            SetupReportOperationException(TransientRetry());
            int calls = 0;

            await sut.InvokeWithRetryAsync(
                _ =>
                {
                    calls++;
                    if (calls == 1) throw clientException;
                    return Task.CompletedTask;
                });

            Assert.Equal(2, calls);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation for func.
        public async Task ThrowsArgumentNullExceptionWhenFuncIsNull()
        {
            var actual = await Assert.ThrowsAsync<ArgumentNullException>(
                () => sut.InvokeWithRetryAsync((Func<ICommunicationClient, Task>)null));
            Assert.Equal(method.Parameter<Func<ICommunicationClient, Task>>().Name, actual.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation for doNotRetryExceptionTypes.
        public async Task ThrowsArgumentNullExceptionWhenDoNotRetryExceptionTypesIsNull()
        {
            var actual = await Assert.ThrowsAsync<ArgumentNullException>(
                () => sut.InvokeWithRetryAsync(_ => throw clientException, (Type[])null));
            Assert.Equal(method.Parameter<Type[]>().Name, actual.ParamName);
        }
    }

    public sealed class TryGetLastResolvedServicePartition : ServicePartitionClientTest
    {
        [Fact]
        public void ReturnsFalseAndNullWhenNoPartitionResolvedYet()
        {
            bool actual = sut.TryGetLastResolvedServicePartition(out ResolvedServicePartition resolvedServicePartition);
            Assert.False(actual);
            Assert.Null(resolvedServicePartition);
        }

        [Fact]
        public async Task ReturnsTrueAndLastRspWhenSet()
        {
            // Drive InvokeWithRetryAsync so the SUT assigns lastRsp from communicationClient.ResolvedServicePartition.
            var rsp = Type<ResolvedServicePartition>.Uninitialized();
            var communicationClient = new Mock<ICommunicationClient>();
            _ = communicationClient.SetupGet(_ => _.ResolvedServicePartition).Returns(rsp);
            _ = communicationClientFactory
                .Setup(_ => _.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()))
                .ReturnsAsync(communicationClient.Object);
            _ = await sut.InvokeWithRetryAsync<object>(_ => Task.FromResult(new object()), TestContext.Current.CancellationToken);

            bool actual = sut.TryGetLastResolvedServicePartition(out ResolvedServicePartition resolvedServicePartition);

            Assert.True(actual);
            Assert.Same(rsp, resolvedServicePartition);
        }
    }

    public abstract class InvokeWithRetryAsyncBase : ServicePartitionClientTest
    {
        protected readonly Mock<ICommunicationClient> communicationClient = new();
        protected readonly ResolvedServicePartition rsp = Type<ResolvedServicePartition>.Uninitialized();

        // Default: GetClientAsync returns the mock client; ReportOperationExceptionAsync handles `clientException`
        // with a transient retry that uses a short delay so tests don't slow down.
        protected static readonly TimeSpan ShortRetryDelay = TimeSpan.FromMilliseconds(1);

        protected readonly Exception clientException = new InvalidOperationException();

        protected ICommunicationClient client => communicationClient.Object;

        protected InvokeWithRetryAsyncBase()
        {
            _ = communicationClient.SetupGet(_ => _.ResolvedServicePartition).Returns(rsp);
            _ = communicationClientFactory
                .Setup(_ => _.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()))
                .ReturnsAsync(client);
            _ = communicationClientFactory
                .Setup(_ => _.GetClientAsync(rsp, targetReplicaSelector, listenerName, retrySettings, It.IsAny<CancellationToken>()))
                .ReturnsAsync(client);
        }

        protected void SetupReportOperationException(OperationRetryControl control, Exception expectedException = null) =>
            _ = communicationClientFactory
                .Setup(_ => _.ReportOperationExceptionAsync(
                    client,
                    It.Is<ExceptionInformation>(i => i.Exception == (expectedException ?? clientException) && i.TargetReplica == targetReplicaSelector),
                    retrySettings,
                    CancellationToken.None))
                .ReturnsAsync(control);

        protected static OperationRetryControl TransientRetry(int maxRetryCount = 5, TimeSpan? delay = null) =>
            new()
            {
                ShouldRetry = true,
                IsTransient = true,
                MaxRetryCount = maxRetryCount,
                ExceptionId = "exception",
                GetRetryDelay = _ => delay ?? ShortRetryDelay,
            };

        protected static OperationRetryControl NonTransientRetry(int maxRetryCount = 5, TimeSpan? delay = null) =>
            new()
            {
                ShouldRetry = true,
                IsTransient = false,
                MaxRetryCount = maxRetryCount,
                ExceptionId = "exception",
                GetRetryDelay = _ => delay ?? ShortRetryDelay,
            };
    }
}
