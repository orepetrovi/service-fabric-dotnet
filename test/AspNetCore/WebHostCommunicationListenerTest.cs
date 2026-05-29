// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.AspNetCore;

public abstract class WebHostCommunicationListenerTest
{
    readonly ICommunicationListener sut;

    // Constructor parameters
    readonly Mock<Func<string, AspNetCoreCommunicationListener, IWebHost>> build = new();
    readonly AspNetCoreCommunicationListener listener;

    readonly string listenerUrl = $"http://+:{fuzzy.UInt16()}";
    readonly StatelessServiceContext serviceContext = fuzzy.StatelessServiceContext();
    readonly Mock<IWebHost> host = new();
    IWebHost buildHost;

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    WebHostCommunicationListenerTest()
    {
        buildHost = host.Object;
        _ = host.Setup(_ => _.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _ = host.Setup(_ => _.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _ = build.Setup(_ => _(It.IsAny<string>(), It.IsAny<AspNetCoreCommunicationListener>())).Returns(() => buildHost);

        listener = new TestListener(serviceContext, build.Object, listenerUrl);
        sut = new WebHostCommunicationListener(build.Object, listener);
    }

    public sealed class Abort : WebHostCommunicationListenerTest
    {
        public Abort() => SetupServer($"http://+:{fuzzy.UInt16()}");

        [Fact]
        public async Task DisposesHostAfterOpenAsync()
        {
            _ = await sut.OpenAsync(CancellationToken.None);
            sut.Abort();
            host.Verify(_ => _.Dispose(), Times.Once());
        }

        [Fact]
        public async Task DoesNotInvokeStopAsync()
        {
            _ = await sut.OpenAsync(CancellationToken.None);
            sut.Abort();
            host.Verify(_ => _.StopAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public void DoesNotInvokeHostBeforeOpenAsync()
        {
            sut.Abort();

            build.Verify(_ => _(It.IsAny<string>(), It.IsAny<AspNetCoreCommunicationListener>()), Times.Never());
        }
    }

    public sealed class CloseAsync : WebHostCommunicationListenerTest
    {
        readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

        public CloseAsync() => SetupServer($"http://+:{fuzzy.UInt16()}");

        [Fact]
        public async Task PassesCancellationTokenToHostStopAsync()
        {
            _ = await sut.OpenAsync(CancellationToken.None);

            await sut.CloseAsync(cancellation);

            host.Verify(_ => _.StopAsync(cancellation), Times.Once());
            host.Verify(_ => _.StopAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task DisposesHostAfterStopAsync()
        {
            _ = await sut.OpenAsync(CancellationToken.None);
            bool stopped = false;
            bool stoppedBeforeDispose = false;
            _ = host.Setup(_ => _.StopAsync(cancellation)).Callback(() => stopped = true).Returns(Task.CompletedTask);
            _ = host.Setup(_ => _.Dispose()).Callback(() => stoppedBeforeDispose = stopped);

            await sut.CloseAsync(cancellation);

            Assert.True(stoppedBeforeDispose, $"{nameof(IDisposable.Dispose)} called before {nameof(IWebHost.StopAsync)}");
            host.Verify(_ => _.Dispose(), Times.Once());
        }

        [Fact]
        public async Task AwaitsHostStopAsyncBeforeReturning()
        {
            _ = await sut.OpenAsync(CancellationToken.None);
            var completion = new TaskCompletionSource<object>();
            _ = host.Setup(_ => _.StopAsync(cancellation)).Returns(completion.Task);

            Task close = sut.CloseAsync(cancellation);

            Assert.False(close.IsCompleted);
            host.Verify(_ => _.Dispose(), Times.Never());
            completion.SetResult(null);
            await close;
            host.Verify(_ => _.Dispose(), Times.Once());
        }

        [Fact]
        public async Task DoesNotInvokeHostBeforeOpenAsync()
        {
            await sut.CloseAsync(cancellation);

            build.Verify(_ => _(It.IsAny<string>(), It.IsAny<AspNetCoreCommunicationListener>()), Times.Never());
        }

        [Fact(Explicit = true)] // TODO: SUT bug. CloseAsync skips Dispose when StopAsync throws.
        public async Task DisposesHostWhenStopAsyncThrows()
        {
            // SUT awaits host.StopAsync then calls Dispose without try/finally,
            // so a faulted stop leaks the host. Expected behavior is to always Dispose.
            _ = await sut.OpenAsync(CancellationToken.None);
            _ = host.Setup(_ => _.StopAsync(cancellation)).ThrowsAsync(new InvalidOperationException());

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CloseAsync(cancellation));

            host.Verify(_ => _.Dispose(), Times.Once());
        }
    }

    public sealed class Constructor : WebHostCommunicationListenerTest
    {
        [Fact(Explicit = true)] // TODO: SUT bug. Missing arg null validation.
        public void ThrowsArgumentNullExceptionWhenBuildIsNull()
        {
            // SUT currently stores `build` without validation and throws NullReferenceException
            // when OpenAsync dereferences it. Expected behavior is to fail fast in the constructor.
            var exception = Assert.Throws<ArgumentNullException>(() => new WebHostCommunicationListener(null, listener));
            Assert.Equal(nameof(build), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing arg null validation.
        public void ThrowsArgumentNullExceptionWhenListenerIsNull()
        {
            // SUT currently dereferences `listener.ServiceContext` without validation,
            // throwing NullReferenceException instead of ArgumentNullException.
            var exception = Assert.Throws<ArgumentNullException>(() => new WebHostCommunicationListener(build.Object, null));
            Assert.Equal(nameof(listener), exception.ParamName);
        }
    }

    public sealed class OpenAsync : WebHostCommunicationListenerTest
    {
        readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

        public OpenAsync() => SetupServer($"http://+:{fuzzy.UInt16()}");

        [Fact]
        public async Task InvokesBuildWithListenerUrlAndListener()
        {
            _ = await sut.OpenAsync(cancellation);

            build.Verify(_ => _(listenerUrl, listener), Times.Once());
            build.Verify(_ => _(It.IsAny<string>(), It.IsAny<AspNetCoreCommunicationListener>()), Times.Once());
        }

        [Fact]
        public async Task ThrowsInvalidOperationExceptionWhenBuildReturnsNull()
        {
            buildHost = null;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.OpenAsync(cancellation));
            Assert.Equal(SR.WebHostNullExceptionMessage, exception.Message);
        }

        [Fact]
        public async Task PassesCancellationTokenToHostStartAsync()
        {
            _ = await sut.OpenAsync(cancellation);

            host.Verify(_ => _.StartAsync(cancellation), Times.Once());
            host.Verify(_ => _.StartAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task AwaitsHostStartAsyncBeforeReturning()
        {
            var start = new TaskCompletionSource<object>();
            _ = host.Setup(_ => _.StartAsync(cancellation)).Returns(start.Task);

            Task<string> open = sut.OpenAsync(cancellation);

            Assert.False(open.IsCompleted);
            start.SetResult(null);
            _ = await open;
        }

        [Fact]
        public async Task ReadsServerFeaturesAfterHostStartAsyncCompletes()
        {
            // Guards against a regression where ServerFeatures is read before StartAsync completes.
            // In real hosting (e.g. Kestrel ":0") addresses are populated during StartAsync,
            // so an early read would return stale/unbound addresses.
            bool started = false;
            bool readBeforeStart = false;
            var start = new TaskCompletionSource<object>();
            _ = host.Setup(_ => _.StartAsync(cancellation)).Returns(start.Task);

            var features = new FeatureCollection();
            features.Set(Mock.Of<IServerAddressesFeature>(_ => _.Addresses == new[] { $"http://+:{fuzzy.UInt16()}" }));
            _ = host.Setup(_ => _.ServerFeatures).Returns(() =>
            {
                readBeforeStart |= !started;
                return features;
            });

            Task<string> open = sut.OpenAsync(cancellation);

            Assert.False(open.IsCompleted);
            started = true;
            start.SetResult(null);
            _ = await open;

            Assert.False(readBeforeStart, $"{nameof(IWebHost.ServerFeatures)} read before host.{nameof(IWebHost.StartAsync)} completed");
        }

        [Fact]
        public async Task ThrowsInvalidOperationExceptionWhenServerHasNoAddresses()
        {
            var features = new FeatureCollection();
            features.Set(Mock.Of<IServerAddressesFeature>(_ => _.Addresses == Array.Empty<string>()));
            _ = host.Setup(_ => _.ServerFeatures).Returns(features);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.OpenAsync(cancellation));
            Assert.Equal(SR.ErrorNoUrlFromAspNetCore, exception.Message);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing null check on IServerAddressesFeature.
        public async Task ThrowsInvalidOperationExceptionWhenServerAddressesFeatureIsNotRegistered()
        {
            // SUT currently dereferences ServerFeatures.Get<IServerAddressesFeature>() without a null check,
            // throwing NullReferenceException instead of InvalidOperationException with SR.ErrorNoUrlFromAspNetCore.
            _ = host.Setup(_ => _.ServerFeatures).Returns(new FeatureCollection());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.OpenAsync(cancellation));
            Assert.Equal(SR.ErrorNoUrlFromAspNetCore, exception.Message);
        }

        [Fact]
        public async Task ReplacesPlusWildcardWithPublishAddress()
        {
            ushort port = fuzzy.UInt16();
            SetupServer($"http://+:{port}");

            string actual = await sut.OpenAsync(cancellation);

            Assert.Equal($"http://{serviceContext.PublishAddress}:{port}", actual);
        }

        [Fact]
        public async Task ReplacesIPv6WildcardWithPublishAddress()
        {
            ushort port = fuzzy.UInt16();
            SetupServer($"http://[::]:{port}");

            string actual = await sut.OpenAsync(cancellation);

            Assert.Equal($"http://{serviceContext.PublishAddress}:{port}", actual);
        }

        [Fact]
        public async Task ReturnsServerAddressUnchangedWhenNoWildcard()
        {
            ushort port = fuzzy.UInt16();
            SetupServer($"http://127.0.0.1:{port}");

            string actual = await sut.OpenAsync(cancellation);

            Assert.Equal($"http://127.0.0.1:{port}", actual);
        }

        [Fact]
        public async Task TrimsTrailingSlashFromServerAddress()
        {
            ushort port = fuzzy.UInt16();
            SetupServer($"http://+:{port}/");

            string actual = await sut.OpenAsync(cancellation);

            Assert.Equal($"http://{serviceContext.PublishAddress}:{port}", actual);
        }

        [Fact]
        public async Task AppendsUrlSuffixFromListener()
        {
            ushort port = fuzzy.UInt16();
            SetupServer($"http://+:{port}");
            listener.ConfigureToUseUniqueServiceUrl();

            string actual = await sut.OpenAsync(cancellation);

            Assert.Equal($"http://{serviceContext.PublishAddress}:{port}{listener.UrlSuffix}", actual);
        }

        [Fact]
        public async Task AppendsUrlSuffixConfiguredDuringBuild()
        {
            // Guards against a regression where UrlSuffix is read before invoking build(...).
            // UseServiceFabricIntegration configures the listener from inside the host-builder delegate,
            // so the suffix isn't known until after build returns.
            ushort port = fuzzy.UInt16();
            SetupServer($"http://+:{port}");
            _ = build.Setup(_ => _(It.IsAny<string>(), It.IsAny<AspNetCoreCommunicationListener>()))
                .Callback((string _, AspNetCoreCommunicationListener l) => l.ConfigureToUseUniqueServiceUrl())
                .Returns(() => buildHost);

            string actual = await sut.OpenAsync(cancellation);

            build.Verify(_ => _(listenerUrl, listener), Times.Once());
            build.Verify(_ => _(It.IsAny<string>(), It.IsAny<AspNetCoreCommunicationListener>()), Times.Once());
            Assert.Equal($"http://{serviceContext.PublishAddress}:{port}{listener.UrlSuffix}", actual);
        }

        [Fact]
        public async Task UsesFirstServerAddressWhenMultipleAreConfigured()
        {
            ushort firstPort = fuzzy.UInt16().Maximum(ushort.MaxValue - 5);
            ushort secondPort = (ushort)(firstPort + fuzzy.SByte().Between(1, 5));
            var features = new FeatureCollection();
            features.Set(Mock.Of<IServerAddressesFeature>(_ => _.Addresses == new[] { $"http://+:{firstPort}", $"http://+:{secondPort}" }));
            _ = host.Setup(_ => _.ServerFeatures).Returns(features);

            string actual = await sut.OpenAsync(cancellation);

            Assert.Equal($"http://{serviceContext.PublishAddress}:{firstPort}", actual);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. OpenAsync skips Dispose when StartAsync throws.
        public async Task DisposesHostWhenStartAsyncThrows()
        {
            // SUT assigns this.host = build(...) before awaiting host.StartAsync without try/finally,
            // so a faulted start leaks the host. Expected behavior is to Dispose the host on failure.
            _ = host.Setup(_ => _.StartAsync(cancellation)).ThrowsAsync(new InvalidOperationException());

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.OpenAsync(cancellation));

            host.Verify(_ => _.Dispose(), Times.Once());
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Second OpenAsync overwrites first host without disposing it.
        public async Task DisposesPreviousHostWhenInvokedTwice()
        {
            // SUT unconditionally assigns this.host = this.build(...) on every OpenAsync call,
            // so a second open overwrites the first reference, leaking the previous host.
            // Expected behavior is to dispose the previous host before replacing it.
            _ = await sut.OpenAsync(cancellation);
            var secondHost = new Mock<IWebHost>();
            _ = secondHost.Setup(_ => _.StartAsync(cancellation)).Returns(Task.CompletedTask);
            _ = secondHost.Setup(_ => _.ServerFeatures).Returns(host.Object.ServerFeatures);
            buildHost = secondHost.Object;

            _ = await sut.OpenAsync(cancellation);

            host.Verify(_ => _.Dispose(), Times.Once());
        }
    }

    void SetupServer(string address)
    {
        var features = new FeatureCollection();
        features.Set(Mock.Of<IServerAddressesFeature>(_ => _.Addresses == new[] { address }));
        _ = host.Setup(_ => _.ServerFeatures).Returns(features);
    }

    sealed class TestListener(ServiceContext serviceContext, Func<string, AspNetCoreCommunicationListener, IWebHost> build, string listenerUrl)
        : AspNetCoreCommunicationListener(serviceContext, build)
    {
        protected internal override string GetListenerUrl() => listenerUrl;
    }
}
