// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using System.Fabric.Description;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.ServiceFabric.AspNetCore.Tests;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.AspNetCore;

public abstract class AspNetCoreCommunicationListenerTest
{
    readonly AspNetCoreCommunicationListener sut;

    // Constructor parameters
    readonly ServiceContext serviceContext = fuzzy.ServiceContext();
    readonly Func<string, AspNetCoreCommunicationListener, IWebHost> build = (_, _) => Mock.Of<IWebHost>();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    AspNetCoreCommunicationListenerTest() =>
        sut = new TestListener(serviceContext, build);

    public sealed class Abort : AspNetCoreCommunicationListenerTest
    {
        [Fact]
        public void DoesNotThrowBeforeOpenAsyncOnGenericHost()
        {
            var fixture = new GenericHostFixture(serviceContext);

            fixture.Sut.Abort();
        }

        [Fact]
        public void DoesNotThrowBeforeOpenAsyncOnWebHost()
        {
            var fixture = new WebHostFixture(serviceContext);

            fixture.Sut.Abort();
        }

        [Fact]
        public async Task InvokesHostDisposeOnGenericHost()
        {
            var fixture = new GenericHostFixture(serviceContext);
            _ = await fixture.Sut.OpenAsync(CancellationToken.None);

            fixture.Sut.Abort();

            fixture.Host.Verify(_ => _.Dispose(), Times.Once());
            fixture.Host.Verify(_ => _.StopAsync(It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public async Task InvokesHostDisposeOnWebHost()
        {
            var fixture = new WebHostFixture(serviceContext);
            _ = await fixture.Sut.OpenAsync(CancellationToken.None);

            fixture.Sut.Abort();

            fixture.Host.Verify(_ => _.Dispose(), Times.Once());
            fixture.Host.Verify(_ => _.StopAsync(It.IsAny<CancellationToken>()), Times.Never());
        }
    }

    public sealed class CloseAsync : AspNetCoreCommunicationListenerTest
    {
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public async Task DoesNotThrowBeforeOpenAsyncOnGenericHost()
        {
            var fixture = new GenericHostFixture(serviceContext);

            await fixture.Sut.CloseAsync(cancellationToken);
        }

        [Fact]
        public async Task DoesNotThrowBeforeOpenAsyncOnWebHost()
        {
            var fixture = new WebHostFixture(serviceContext);

            await fixture.Sut.CloseAsync(cancellationToken);
        }

        [Fact]
        public async Task PassesCancellationTokenToHostStopAsyncOnGenericHost()
        {
            var fixture = new GenericHostFixture(serviceContext);
            _ = await fixture.Sut.OpenAsync(CancellationToken.None);

            await fixture.Sut.CloseAsync(cancellationToken);

            fixture.Host.Verify(_ => _.StopAsync(cancellationToken), Times.Once());
            fixture.Host.Verify(_ => _.StopAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task PassesCancellationTokenToHostStopAsyncOnWebHost()
        {
            var fixture = new WebHostFixture(serviceContext);
            _ = await fixture.Sut.OpenAsync(CancellationToken.None);

            await fixture.Sut.CloseAsync(cancellationToken);

            fixture.Host.Verify(_ => _.StopAsync(cancellationToken), Times.Once());
            fixture.Host.Verify(_ => _.StopAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task DisposesHostAfterStopAsyncOnGenericHost()
        {
            var fixture = new GenericHostFixture(serviceContext);
            _ = await fixture.Sut.OpenAsync(CancellationToken.None);
            bool stopped = false;
            _ = fixture.Host.Setup(_ => _.StopAsync(cancellationToken)).Callback(() => stopped = true).Returns(Task.FromResult<object>(null));
            _ = fixture.Host.Setup(_ => _.Dispose()).Callback(() => Assert.True(stopped, "Dispose called before StopAsync"));

            await fixture.Sut.CloseAsync(cancellationToken);

            fixture.Host.Verify(_ => _.Dispose(), Times.Once());
        }

        [Fact]
        public async Task DisposesHostAfterStopAsyncOnWebHost()
        {
            var fixture = new WebHostFixture(serviceContext);
            _ = await fixture.Sut.OpenAsync(CancellationToken.None);
            bool stopped = false;
            _ = fixture.Host.Setup(_ => _.StopAsync(cancellationToken)).Callback(() => stopped = true).Returns(Task.FromResult<object>(null));
            _ = fixture.Host.Setup(_ => _.Dispose()).Callback(() => Assert.True(stopped, "Dispose called before StopAsync"));

            await fixture.Sut.CloseAsync(cancellationToken);

            fixture.Host.Verify(_ => _.Dispose(), Times.Once());
        }
    }

    public sealed class ConfigureToUseUniqueServiceUrl : AspNetCoreCommunicationListenerTest
    {
        [Fact]
        public void AppendsPartitionAndInstanceToUrlSuffixForStatelessContext()
        {
            StatelessServiceContext context = fuzzy.StatelessServiceContext();
            var listener = new TestListener(context, build);

            listener.ConfigureToUseUniqueServiceUrl();

            string expected = string.Format(CultureInfo.InvariantCulture, "/{0}/{1}", context.PartitionId, context.ReplicaOrInstanceId);
            Assert.Equal(expected, listener.UrlSuffix);
        }

        [Fact]
        public void AppendsPartitionReplicaAndNonEmptyGuidToUrlSuffixForStatefulContext()
        {
            StatefulServiceContext context = fuzzy.StatefulServiceContext();
            var listener = new TestListener(context, build);

            listener.ConfigureToUseUniqueServiceUrl();

            string prefix = string.Format(CultureInfo.InvariantCulture, "/{0}/{1}/", context.PartitionId, context.ReplicaOrInstanceId);
            Assert.StartsWith(prefix, listener.UrlSuffix);
            Guid trailing = Guid.Parse(listener.UrlSuffix.Substring(prefix.Length));
            Assert.NotEqual(Guid.Empty, trailing);
        }

        [Fact]
        public void DoesNotChangeUrlSuffixOnSecondCall()
        {
            StatefulServiceContext context = fuzzy.StatefulServiceContext();
            var listener = new TestListener(context, build);
            listener.ConfigureToUseUniqueServiceUrl();
            string first = listener.UrlSuffix;

            listener.ConfigureToUseUniqueServiceUrl();

            Assert.Equal(first, listener.UrlSuffix);
        }
    }

    public sealed class Constructor_ServiceContext_FuncOfStringOfAspNetCoreCommunicationListenerOfIHost : AspNetCoreCommunicationListenerTest
    {
        new readonly AspNetCoreCommunicationListener sut;

        new readonly Func<string, AspNetCoreCommunicationListener, IHost> build = (_, _) => Mock.Of<IHost>();

        public Constructor_ServiceContext_FuncOfStringOfAspNetCoreCommunicationListenerOfIHost() =>
            sut = new TestListener(serviceContext, build);

        [Fact]
        public void InitializesProperties()
        {
            Assert.Empty(sut.UrlSuffix);
            Assert.Same(serviceContext, sut.ServiceContext);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenServiceContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TestListener(null, build));
            Assert.Equal(nameof(serviceContext), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuildIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new TestListener(serviceContext, (Func<string, AspNetCoreCommunicationListener, IHost>)null));
            Assert.Equal(nameof(build), exception.ParamName);
        }
    }

    public sealed class Constructor_ServiceContext_FuncOfStringOfAspNetCoreCommunicationListenerOfIWebHost : AspNetCoreCommunicationListenerTest
    {
        [Fact]
        public void InitializesProperties()
        {
            Assert.Empty(sut.UrlSuffix);
            Assert.Same(serviceContext, sut.ServiceContext);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenServiceContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TestListener(null, build));
            Assert.Equal(nameof(serviceContext), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuildIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new TestListener(serviceContext, (Func<string, AspNetCoreCommunicationListener, IWebHost>)null));
            Assert.Equal(nameof(build), exception.ParamName);
        }
    }

    public sealed class GetEndpointResourceDescription : AspNetCoreCommunicationListenerTest
    {
        // TestMocksRepository wires an endpoint collection into the mocked ICodePackageActivationContext
        // that these tests mutate; fuzzy.StatelessServiceContext() does not provide that plumbing.
        readonly StatelessServiceContext context = TestMocksRepository.GetMockStatelessServiceContext();
        readonly TestListener listener;

        // Method parameters
        readonly string endpointName = fuzzy.String();

        public GetEndpointResourceDescription() =>
            listener = new TestListener(context, build);

        [Fact]
        public void ReturnsEndpointResourceDescriptionFromManifest()
        {
            var endpoint = new EndpointResourceDescription { Name = endpointName };
            context.CodePackageActivationContext.GetEndpoints().Add(endpoint);

            EndpointResourceDescription actual = listener.GetEndpointResourceDescription(endpoint.Name);

            Assert.Same(endpoint, actual);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenEndpointNameIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => listener.GetEndpointResourceDescription(null));
            Assert.Equal(nameof(endpointName), exception.ParamName);
        }

        [Fact]
        public void ThrowsInvalidOperationExceptionWhenEndpointIsNotInManifest() =>
            _ = Assert.Throws<InvalidOperationException>(() => listener.GetEndpointResourceDescription(endpointName));
    }

    public sealed class OpenAsync : AspNetCoreCommunicationListenerTest
    {
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact]
        public async Task InvokesBuildDelegateWithGetListenerUrlAndSelfOnGenericHost()
        {
            string listenerUrl = "http://+:" + fuzzy.UInt16();
            var fixture = new GenericHostFixture(serviceContext, listenerUrl);

            _ = await fixture.Sut.OpenAsync(cancellationToken);

            Assert.Equal(listenerUrl, fixture.BuildUrl);
            Assert.Same(fixture.Sut, fixture.BuildListener);
        }

        [Fact]
        public async Task InvokesBuildDelegateWithGetListenerUrlAndSelfOnWebHost()
        {
            string listenerUrl = "http://+:" + fuzzy.UInt16();
            var fixture = new WebHostFixture(serviceContext, listenerUrl);

            _ = await fixture.Sut.OpenAsync(cancellationToken);

            Assert.Equal(listenerUrl, fixture.BuildUrl);
            Assert.Same(fixture.Sut, fixture.BuildListener);
        }

        [Fact]
        public async Task PassesCancellationTokenToHostStartAsyncOnGenericHost()
        {
            var fixture = new GenericHostFixture(serviceContext);

            _ = await fixture.Sut.OpenAsync(cancellationToken);

            fixture.Host.Verify(_ => _.StartAsync(cancellationToken), Times.Once());
        }

        [Fact]
        public async Task PassesCancellationTokenToHostStartAsyncOnWebHost()
        {
            var fixture = new WebHostFixture(serviceContext);

            _ = await fixture.Sut.OpenAsync(cancellationToken);

            fixture.Host.Verify(_ => _.StartAsync(cancellationToken), Times.Once());
        }

        [Fact]
        public async Task ReturnsUrlFromServerAddressFeatureOnGenericHost()
        {
            StatelessServiceContext context = fuzzy.StatelessServiceContext();
            ushort serverPort = fuzzy.UInt16();
            var fixture = new GenericHostFixture(context, "http://+:" + fuzzy.UInt16(), "http://+:" + serverPort + "/");

            string actual = await fixture.Sut.OpenAsync(cancellationToken);

            Assert.Equal($"http://{context.PublishAddress}:{serverPort}", actual);
        }

        [Fact]
        public async Task ReturnsUrlFromServerAddressFeatureOnWebHost()
        {
            StatelessServiceContext context = fuzzy.StatelessServiceContext();
            ushort serverPort = fuzzy.UInt16();
            var fixture = new WebHostFixture(context, "http://+:" + fuzzy.UInt16(), "http://+:" + serverPort + "/");

            string actual = await fixture.Sut.OpenAsync(cancellationToken);

            Assert.Equal($"http://{context.PublishAddress}:{serverPort}", actual);
        }

        [Fact]
        public async Task AppendsUrlSuffixToReturnedUrlOnGenericHost()
        {
            StatelessServiceContext context = fuzzy.StatelessServiceContext();
            var fixture = new GenericHostFixture(context, "http://+:" + fuzzy.UInt16() + "/");
            fixture.Sut.ConfigureToUseUniqueServiceUrl();

            string actual = await fixture.Sut.OpenAsync(cancellationToken);

            string expected = string.Format(CultureInfo.InvariantCulture, "/{0}/{1}", context.PartitionId, context.ReplicaOrInstanceId);
            Assert.EndsWith(expected, actual);
        }

        [Fact]
        public async Task AppendsUrlSuffixToReturnedUrlOnWebHost()
        {
            StatelessServiceContext context = fuzzy.StatelessServiceContext();
            var fixture = new WebHostFixture(context, "http://+:" + fuzzy.UInt16() + "/");
            fixture.Sut.ConfigureToUseUniqueServiceUrl();

            string actual = await fixture.Sut.OpenAsync(cancellationToken);

            string expected = string.Format(CultureInfo.InvariantCulture, "/{0}/{1}", context.PartitionId, context.ReplicaOrInstanceId);
            Assert.EndsWith(expected, actual);
        }
    }

    sealed class WebHostFixture
    {
        public readonly Mock<IWebHost> Host = new();
        public readonly TestListener Sut;
        public string BuildUrl;
        public AspNetCoreCommunicationListener BuildListener;

        public WebHostFixture(ServiceContext context, string listenerUrl = "http://+:0", string serverAddress = null)
        {
            _ = Host.Setup(_ => _.StartAsync(It.IsAny<CancellationToken>())).Returns(CompletedTask());
            _ = Host.Setup(_ => _.StopAsync(It.IsAny<CancellationToken>())).Returns(CompletedTask());
            Sut = new TestListener(context, (url, listener) =>
            {
                BuildUrl = url;
                BuildListener = listener;
                var features = new FeatureCollection();
                features.Set(Mock.Of<IServerAddressesFeature>(_ => _.Addresses == new[] { serverAddress ?? url }));
                _ = Host.Setup(_ => _.ServerFeatures).Returns(features);
                return Host.Object;
            })
            { ListenerUrl = listenerUrl };
        }

        static Task CompletedTask()
        {
            var tcs = new TaskCompletionSource<object>();
            tcs.SetResult(null);
            return tcs.Task;
        }
    }

    sealed class GenericHostFixture
    {
        public readonly Mock<IHost> Host = new();
        public readonly TestListener Sut;
        public string BuildUrl;
        public AspNetCoreCommunicationListener BuildListener;

        public GenericHostFixture(ServiceContext context, string listenerUrl = "http://+:0", string serverAddress = null)
        {
            _ = Host.Setup(_ => _.StartAsync(It.IsAny<CancellationToken>())).Returns(CompletedTask());
            _ = Host.Setup(_ => _.StopAsync(It.IsAny<CancellationToken>())).Returns(CompletedTask());
            Sut = new TestListener(context, (url, listener) =>
            {
                BuildUrl = url;
                BuildListener = listener;
                var features = new FeatureCollection();
                features.Set(Mock.Of<IServerAddressesFeature>(_ => _.Addresses == new[] { serverAddress ?? url }));
                var server = Mock.Of<IServer>(_ => _.Features == features);
                var services = Mock.Of<IServiceProvider>(_ => _.GetService(typeof(IServer)) == server);
                _ = Host.Setup(_ => _.Services).Returns(services);
                return Host.Object;
            })
            { ListenerUrl = listenerUrl };
        }

        static Task CompletedTask()
        {
            var tcs = new TaskCompletionSource<object>();
            tcs.SetResult(null);
            return tcs.Task;
        }
    }

    sealed class TestListener : AspNetCoreCommunicationListener
    {
        public TestListener(ServiceContext serviceContext, Func<string, AspNetCoreCommunicationListener, IWebHost> build)
            : base(serviceContext, build)
        {
        }

        public TestListener(ServiceContext serviceContext, Func<string, AspNetCoreCommunicationListener, IHost> build)
            : base(serviceContext, build)
        {
        }

        public string ListenerUrl { get; set; } = "http://+:0";

        protected internal override string GetListenerUrl() =>
            ListenerUrl;

        internal new EndpointResourceDescription GetEndpointResourceDescription(string endpointName) =>
            base.GetEndpointResourceDescription(endpointName);
    }
}
