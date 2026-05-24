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

    public sealed class AbortWithGenericHost : AspNetCoreCommunicationListenerTest
    {
        readonly GenericHostFixture fixture;
        readonly Mock<IHost> host;
        new readonly AspNetCoreCommunicationListener sut;

        public AbortWithGenericHost()
        {
            fixture = new GenericHostFixture(serviceContext);
            host = fixture.Host;
            sut = fixture.Sut;
        }

        [Fact]
        public void DoesNotThrowBeforeOpenAsync() =>
            sut.Abort();

        [Fact]
        public async Task DisposesHostAfterOpenAsync()
        {
            _ = await sut.OpenAsync(CancellationToken.None);

            sut.Abort();

            host.Verify(_ => _.Dispose(), Times.Once());
        }
    }

    public sealed class AbortWithWebHost : AspNetCoreCommunicationListenerTest
    {
        readonly WebHostFixture fixture;
        readonly Mock<IWebHost> host;
        new readonly AspNetCoreCommunicationListener sut;

        public AbortWithWebHost()
        {
            fixture = new WebHostFixture(serviceContext);
            host = fixture.Host;
            sut = fixture.Sut;
        }

        [Fact]
        public void DoesNotThrowBeforeOpenAsync() =>
            sut.Abort();

        [Fact]
        public async Task DisposesHostAfterOpenAsync()
        {
            _ = await sut.OpenAsync(CancellationToken.None);

            sut.Abort();

            host.Verify(_ => _.Dispose(), Times.Once());
        }
    }

    public sealed class CloseAsyncWithGenericHost : AspNetCoreCommunicationListenerTest
    {
        readonly GenericHostFixture fixture;
        readonly Mock<IHost> host;
        new readonly AspNetCoreCommunicationListener sut;

        readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

        public CloseAsyncWithGenericHost()
        {
            fixture = new GenericHostFixture(serviceContext);
            host = fixture.Host;
            sut = fixture.Sut;
        }

        [Fact]
        public async Task DoesNotThrowBeforeOpenAsync() =>
            await sut.CloseAsync(cancellationToken);

        [Fact]
        public async Task PassesCancellationTokenToHostStopAsyncAfterOpenAsync()
        {
            _ = await sut.OpenAsync(CancellationToken.None);

            await sut.CloseAsync(cancellationToken);

            host.Verify(_ => _.StopAsync(cancellationToken), Times.Once());
            host.Verify(_ => _.Dispose(), Times.Once());
        }
    }

    public sealed class CloseAsyncWithWebHost : AspNetCoreCommunicationListenerTest
    {
        readonly WebHostFixture fixture;
        readonly Mock<IWebHost> host;
        new readonly AspNetCoreCommunicationListener sut;

        readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

        public CloseAsyncWithWebHost()
        {
            fixture = new WebHostFixture(serviceContext);
            host = fixture.Host;
            sut = fixture.Sut;
        }

        [Fact]
        public async Task DoesNotThrowBeforeOpenAsync() =>
            await sut.CloseAsync(cancellationToken);

        [Fact]
        public async Task PassesCancellationTokenToHostStopAsyncAfterOpenAsync()
        {
            _ = await sut.OpenAsync(CancellationToken.None);

            await sut.CloseAsync(cancellationToken);

            host.Verify(_ => _.StopAsync(cancellationToken), Times.Once());
            host.Verify(_ => _.Dispose(), Times.Once());
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

        [Fact]
        public void InitializesProperties()
        {
            Assert.Equal(string.Empty, sut.UrlSuffix);
            Assert.Same(serviceContext, sut.ServiceContext);
        }
    }

    public sealed class Constructor_ServiceContext_FuncOfStringOfAspNetCoreCommunicationListenerOfIWebHost : AspNetCoreCommunicationListenerTest
    {
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

        [Fact]
        public void InitializesProperties()
        {
            Assert.Equal(string.Empty, sut.UrlSuffix);
            Assert.Same(serviceContext, sut.ServiceContext);
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
        public void ThrowsArgumentNullExceptionWhenEndpointNameIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => listener.GetEndpointResourceDescription(null));
            Assert.Equal(nameof(endpointName), exception.ParamName);
        }

        [Fact]
        public void ThrowsInvalidOperationExceptionWhenEndpointIsNotInManifest() =>
            Assert.Throws<InvalidOperationException>(() => listener.GetEndpointResourceDescription(endpointName));

        [Fact]
        public void ReturnsEndpointResourceDescriptionFromManifest()
        {
            var endpoint = new EndpointResourceDescription { Name = endpointName };
            context.CodePackageActivationContext.GetEndpoints().Add(endpoint);

            EndpointResourceDescription actual = listener.GetEndpointResourceDescription(endpoint.Name);

            Assert.Same(endpoint, actual);
        }
    }

    public sealed class OpenAsyncWithGenericHost : AspNetCoreCommunicationListenerTest
    {
        readonly GenericHostFixture fixture;
        readonly Mock<IHost> host;
        new readonly AspNetCoreCommunicationListener sut;

        readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

        public OpenAsyncWithGenericHost()
        {
            fixture = new GenericHostFixture(serviceContext);
            host = fixture.Host;
            sut = fixture.Sut;
        }

        [Fact]
        public async Task InvokesBuildDelegateWithGetListenerUrlAndSelf()
        {
            _ = await sut.OpenAsync(cancellationToken);

            Assert.Equal(((TestListener)sut).GetListenerUrl(), fixture.BuildUrl);
            Assert.Same(sut, fixture.BuildListener);
        }

        [Fact]
        public async Task PassesCancellationTokenToHostStartAsync()
        {
            _ = await sut.OpenAsync(cancellationToken);

            host.Verify(_ => _.StartAsync(cancellationToken), Times.Once());
        }
    }

    public sealed class OpenAsyncWithWebHost : AspNetCoreCommunicationListenerTest
    {
        readonly WebHostFixture fixture;
        readonly Mock<IWebHost> host;
        new readonly AspNetCoreCommunicationListener sut;

        readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

        public OpenAsyncWithWebHost()
        {
            fixture = new WebHostFixture(serviceContext);
            host = fixture.Host;
            sut = fixture.Sut;
        }

        [Fact]
        public async Task InvokesBuildDelegateWithGetListenerUrlAndSelf()
        {
            _ = await sut.OpenAsync(cancellationToken);

            Assert.Equal(((TestListener)sut).GetListenerUrl(), fixture.BuildUrl);
            Assert.Same(sut, fixture.BuildListener);
        }

        [Fact]
        public async Task PassesCancellationTokenToHostStartAsync()
        {
            _ = await sut.OpenAsync(cancellationToken);

            host.Verify(_ => _.StartAsync(cancellationToken), Times.Once());
        }
    }

    sealed class WebHostFixture
    {
        public Mock<IWebHost> Host { get; } = new();
        public TestListener Sut { get; }
        public string BuildUrl { get; private set; }
        public AspNetCoreCommunicationListener BuildListener { get; private set; }

        public WebHostFixture(ServiceContext context, string listenerUrl = "http://+:0")
        {
            _ = Host.Setup(_ => _.StartAsync(It.IsAny<CancellationToken>())).Returns(CompletedTask());
            _ = Host.Setup(_ => _.StopAsync(It.IsAny<CancellationToken>())).Returns(CompletedTask());
            Sut = new TestListener(context, (url, listener) =>
            {
                BuildUrl = url;
                BuildListener = listener;
                var addresses = new Mock<IServerAddressesFeature>();
                _ = addresses.Setup(_ => _.Addresses).Returns(new[] { url });
                var features = new FeatureCollection();
                features.Set(addresses.Object);
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
        public Mock<IHost> Host { get; } = new();
        public TestListener Sut { get; }
        public string BuildUrl { get; private set; }
        public AspNetCoreCommunicationListener BuildListener { get; private set; }

        public GenericHostFixture(ServiceContext context, string listenerUrl = "http://+:0")
        {
            _ = Host.Setup(_ => _.StartAsync(It.IsAny<CancellationToken>())).Returns(CompletedTask());
            _ = Host.Setup(_ => _.StopAsync(It.IsAny<CancellationToken>())).Returns(CompletedTask());
            Sut = new TestListener(context, (url, listener) =>
            {
                BuildUrl = url;
                BuildListener = listener;
                var addresses = new Mock<IServerAddressesFeature>();
                _ = addresses.Setup(_ => _.Addresses).Returns(new[] { url });
                var features = new FeatureCollection();
                features.Set(addresses.Object);
                var server = new Mock<IServer>();
                _ = server.Setup(_ => _.Features).Returns(features);
                var services = new Mock<IServiceProvider>();
                _ = services.Setup(_ => _.GetService(typeof(IServer))).Returns(server.Object);
                _ = Host.Setup(_ => _.Services).Returns(services.Object);
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
