// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.AspNetCore;

public abstract class GenericHostCommunicationListenerTest
{
    readonly GenericHostCommunicationListener sut;

    // Constructor parameters
    readonly Func<string, AspNetCoreCommunicationListener, IHost> build;
    readonly AspNetCoreCommunicationListener listener;

    readonly StatelessServiceContext serviceContext = fuzzy.StatelessServiceContext();
    readonly Mock<IHost> host = new();
    IHost buildHost;
    string buildUrl;
    AspNetCoreCommunicationListener buildListener;

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    GenericHostCommunicationListenerTest()
    {
        buildHost = host.Object;
        var startTcs = new TaskCompletionSource<object>();
        startTcs.SetResult(null);
        var stopTcs = new TaskCompletionSource<object>();
        stopTcs.SetResult(null);
        _ = host.Setup(_ => _.StartAsync(It.IsAny<CancellationToken>())).Returns(startTcs.Task);
        _ = host.Setup(_ => _.StopAsync(It.IsAny<CancellationToken>())).Returns(stopTcs.Task);

        build = (url, l) =>
        {
            buildUrl = url;
            buildListener = l;
            return buildHost;
        };
        listener = new TestListener(serviceContext, build);
        sut = new GenericHostCommunicationListener(build, listener);
        SetupServer("http://+:80");
    }

    void SetupServer(string address)
    {
        var features = new FeatureCollection();
        features.Set(Mock.Of<IServerAddressesFeature>(_ => _.Addresses == new[] { address }));
        SetupServer(Mock.Of<IServer>(_ => _.Features == features));
    }

    void SetupServer(IServer server)
    {
        var services = Mock.Of<IServiceProvider>(_ => _.GetService(typeof(IServer)) == server);
        _ = host.Setup(_ => _.Services).Returns(services);
    }

    public sealed class Abort : GenericHostCommunicationListenerTest
    {
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
        public void DoesNotThrowBeforeOpenAsync() =>
            sut.Abort();
    }

    public sealed class CloseAsync : GenericHostCommunicationListenerTest
    {
        readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

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
            var stopTcs = new TaskCompletionSource<object>();
            stopTcs.SetResult(null);
            _ = host.Setup(_ => _.StopAsync(cancellation)).Callback(() => stopped = true).Returns(stopTcs.Task);
            _ = host.Setup(_ => _.Dispose()).Callback(() => stoppedBeforeDispose = stopped);

            await sut.CloseAsync(cancellation);

            Assert.True(stoppedBeforeDispose, "Dispose called before StopAsync");
            host.Verify(_ => _.Dispose(), Times.Once());
        }

        [Fact]
        public async Task AwaitsHostStopAsyncBeforeReturning()
        {
            _ = await sut.OpenAsync(CancellationToken.None);
            var tcs = new TaskCompletionSource<object>();
            _ = host.Setup(_ => _.StopAsync(cancellation)).Returns(tcs.Task);

            Task closeTask = sut.CloseAsync(cancellation);

            Assert.False(closeTask.IsCompleted);
            host.Verify(_ => _.Dispose(), Times.Never());
            tcs.SetResult(null);
            await closeTask;
            host.Verify(_ => _.Dispose(), Times.Once());
        }

        [Fact]
        public async Task DoesNotThrowBeforeOpenAsync() =>
            await sut.CloseAsync(cancellation);
    }

    public sealed class OpenAsync : GenericHostCommunicationListenerTest
    {
        readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

        [Fact]
        public async Task InvokesBuildDelegateWithListenerUrlAndListener()
        {
            string listenerUrl = "http://+:" + fuzzy.UInt16();
            ((TestListener)listener).ListenerUrl = listenerUrl;

            _ = await sut.OpenAsync(cancellation);

            Assert.Equal(listenerUrl, buildUrl);
            Assert.Same(listener, buildListener);
        }

        [Fact]
        public async Task ThrowsInvalidOperationExceptionWhenBuildReturnsNull()
        {
            buildHost = null;

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.OpenAsync(cancellation));
            Assert.Equal(SR.HostNullExceptionMessage, exception.Message);
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
            var tcs = new TaskCompletionSource<object>();
            _ = host.Setup(_ => _.StartAsync(cancellation)).Returns(tcs.Task);

            Task<string> openTask = sut.OpenAsync(cancellation);

            Assert.False(openTask.IsCompleted);
            tcs.SetResult(null);
            await openTask;
        }

        [Fact]
        public async Task ThrowsInvalidOperationExceptionWhenServerIsNotRegistered()
        {
            SetupServer((IServer)null);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.OpenAsync(cancellation));
            Assert.Equal(SR.WebServerNotFound, exception.Message);
        }

        [Fact]
        public async Task ThrowsInvalidOperationExceptionWhenAddressIsNull()
        {
            var features = new FeatureCollection();
            features.Set(Mock.Of<IServerAddressesFeature>(_ => _.Addresses == Array.Empty<string>()));
            SetupServer(Mock.Of<IServer>(_ => _.Features == features));

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.OpenAsync(cancellation));
            Assert.Equal(SR.ErrorNoUrlFromAspNetCore, exception.Message);
        }

        [Fact]
        public async Task ReplacesPlusWildcardWithPublishAddress()
        {
            ushort port = fuzzy.UInt16();
            SetupServer("http://+:" + port);

            string actual = await sut.OpenAsync(cancellation);

            Assert.Equal($"http://{serviceContext.PublishAddress}:{port}", actual);
        }

        [Fact]
        public async Task ReplacesIPv6WildcardWithPublishAddress()
        {
            ushort port = fuzzy.UInt16();
            SetupServer("http://[::]:" + port);

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
            SetupServer("http://+:" + port + "/");

            string actual = await sut.OpenAsync(cancellation);

            Assert.Equal($"http://{serviceContext.PublishAddress}:{port}", actual);
        }

        [Fact]
        public async Task AppendsUrlSuffixFromListener()
        {
            ushort port = fuzzy.UInt16();
            SetupServer("http://+:" + port);
            listener.ConfigureToUseUniqueServiceUrl();

            string actual = await sut.OpenAsync(cancellation);

            Assert.Equal($"http://{serviceContext.PublishAddress}:{port}{listener.UrlSuffix}", actual);
        }
    }

    sealed class TestListener : AspNetCoreCommunicationListener
    {
        internal TestListener(ServiceContext serviceContext, Func<string, AspNetCoreCommunicationListener, IHost> build)
            : base(serviceContext, build)
        {
        }

        internal string ListenerUrl = "http://+:0";

        protected internal override string GetListenerUrl() =>
            ListenerUrl;
    }
}
