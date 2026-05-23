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
    readonly Func<string, AspNetCoreCommunicationListener, IWebHost> build;

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    readonly Mock<IWebHost> webHost = CreateWebHost();
    string buildUrl;
    AspNetCoreCommunicationListener buildListener;

    AspNetCoreCommunicationListenerTest()
    {
        build = (url, listener) =>
        {
            buildUrl = url;
            buildListener = listener;
            SetupAddresses(webHost, url);
            return webHost.Object;
        };
        sut = new TestListener(serviceContext, build);
    }

    public sealed class Abort : AspNetCoreCommunicationListenerTest
    {
        [Fact]
        public void DoesNotThrowBeforeOpenAsync() =>
            sut.Abort();

        [Fact]
        public async Task DisposesHostAfterOpenAsync()
        {
            _ = await sut.OpenAsync(CancellationToken.None);

            sut.Abort();

            webHost.Verify(_ => _.Dispose(), Times.Once);
        }
    }

    public sealed class CloseAsync : AspNetCoreCommunicationListenerTest
    {
        readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

        [Fact]
        public async Task DoesNotThrowBeforeOpenAsync() =>
            await sut.CloseAsync(cancellationToken);

        [Fact]
        public async Task PassesCancellationTokenToHostStopAsyncAfterOpenAsync()
        {
            _ = await sut.OpenAsync(CancellationToken.None);

            await sut.CloseAsync(cancellationToken);

            webHost.Verify(_ => _.StopAsync(cancellationToken), Times.Once);
        }
    }

    public sealed class ConfigureToUseUniqueServiceUrl : AspNetCoreCommunicationListenerTest
    {
        [Fact]
        public void AppendsPartitionAndInstanceToUrlSuffixForStatelessContext()
        {
            StatelessServiceContext context = TestMocksRepository.GetMockStatelessServiceContext();
            var listener = new TestListener(context, build);

            listener.ConfigureToUseUniqueServiceUrl();

            string expected = string.Format(CultureInfo.InvariantCulture, "/{0}/{1}", context.PartitionId, context.ReplicaOrInstanceId);
            Assert.Equal(expected, listener.UrlSuffix);
        }

        [Fact]
        public void AppendsPartitionReplicaAndNonEmptyGuidToUrlSuffixForStatefulContext()
        {
            StatefulServiceContext context = TestMocksRepository.GetMockStatefulServiceContext();
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
            StatefulServiceContext context = TestMocksRepository.GetMockStatefulServiceContext();
            var listener = new TestListener(context, build);
            listener.ConfigureToUseUniqueServiceUrl();
            string first = listener.UrlSuffix;

            listener.ConfigureToUseUniqueServiceUrl();

            Assert.Equal(first, listener.UrlSuffix);
        }
    }

    public sealed class Constructor_ServiceContext_FuncOfStringAspNetCoreCommunicationListenerIHost : AspNetCoreCommunicationListenerTest
    {
        new readonly AspNetCoreCommunicationListener sut;

        new readonly Func<string, AspNetCoreCommunicationListener, IHost> build = (_, _) => Mock.Of<IHost>();

        public Constructor_ServiceContext_FuncOfStringAspNetCoreCommunicationListenerIHost() =>
            sut = new TestListener(serviceContext, build);

        [Fact]
        public void ThrowsArgumentNullExceptionWhenServiceContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TestListener(null, build));
            Assert.Equal("serviceContext", exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuildIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new TestListener(serviceContext, (Func<string, AspNetCoreCommunicationListener, IHost>)null));
            Assert.Equal("build", exception.ParamName);
        }

        [Fact]
        public void InitializesProperties()
        {
            Assert.Equal(string.Empty, sut.UrlSuffix);
            Assert.Same(serviceContext, sut.ServiceContext);
        }
    }

    public sealed class Constructor_ServiceContext_FuncOfStringAspNetCoreCommunicationListenerIWebHost : AspNetCoreCommunicationListenerTest
    {
        [Fact]
        public void ThrowsArgumentNullExceptionWhenServiceContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new TestListener(null, build));
            Assert.Equal("serviceContext", exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuildIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new TestListener(serviceContext, (Func<string, AspNetCoreCommunicationListener, IWebHost>)null));
            Assert.Equal("build", exception.ParamName);
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
        readonly StatelessServiceContext context = TestMocksRepository.GetMockStatelessServiceContext();
        readonly TestListener listener;

        public GetEndpointResourceDescription() =>
            listener = new TestListener(context, build);

        [Fact]
        public void ThrowsArgumentNullExceptionWhenEndpointNameIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => listener.GetEndpointResourceDescription(null));
            Assert.Equal("endpointName", exception.ParamName);
        }

        [Fact]
        public void ThrowsInvalidOperationExceptionWhenEndpointIsNotInManifest() =>
            Assert.Throws<InvalidOperationException>(() => listener.GetEndpointResourceDescription(fuzzy.String()));

        [Fact]
        public void ReturnsEndpointResourceDescriptionFromManifest()
        {
            var endpoint = new EndpointResourceDescription { Name = fuzzy.String() };
            context.CodePackageActivationContext.GetEndpoints().Add(endpoint);

            EndpointResourceDescription actual = listener.GetEndpointResourceDescription(endpoint.Name);

            Assert.Same(endpoint, actual);
        }
    }

    public sealed class OpenAsync : AspNetCoreCommunicationListenerTest
    {
        readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

        [Fact]
        public async Task InvokesBuildDelegateWithGetListenerUrlAndSelf()
        {
            _ = await sut.OpenAsync(cancellationToken);

            Assert.Equal(((TestListener)sut).GetListenerUrl(), buildUrl);
            Assert.Same(sut, buildListener);
        }

        [Fact]
        public async Task PassesCancellationTokenToHostStartAsync()
        {
            _ = await sut.OpenAsync(cancellationToken);

            webHost.Verify(_ => _.StartAsync(cancellationToken), Times.Once);
        }

        [Fact]
        public async Task ReturnsTaskThatCompletesWithUrl()
        {
            Task<string> task = sut.OpenAsync(cancellationToken);

            Assert.NotNull(task);
            string url = await task;
            Assert.False(string.IsNullOrEmpty(url));
        }
    }

    static Mock<IWebHost> CreateWebHost()
    {
        var host = new Mock<IWebHost>();
        _ = host.Setup(_ => _.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _ = host.Setup(_ => _.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return host;
    }

    static void SetupAddresses(Mock<IWebHost> host, string url)
    {
        var addresses = new Mock<IServerAddressesFeature>();
        _ = addresses.Setup(_ => _.Addresses).Returns(new[] { url });
        var features = new FeatureCollection();
        features.Set(addresses.Object);
        _ = host.Setup(_ => _.ServerFeatures).Returns(features);
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
