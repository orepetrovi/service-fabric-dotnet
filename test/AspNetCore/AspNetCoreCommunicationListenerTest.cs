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

    public sealed class Constructor_ServiceContext_FuncOfStringAspNetCoreCommunicationListenerIWebHost : AspNetCoreCommunicationListenerTest
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

    public abstract class Abort
    {
        protected abstract AspNetCoreCommunicationListener Sut { get; }

        protected abstract void VerifyHostDisposed(Times times);

        [Fact]
        public void DoesNotThrowBeforeOpenAsync() =>
            Sut.Abort();

        [Fact]
        public async Task DisposesHostAfterOpenAsync()
        {
            _ = await Sut.OpenAsync(CancellationToken.None);

            Sut.Abort();

            VerifyHostDisposed(Times.Once());
        }

        public sealed class WithWebHost : Abort
        {
            readonly WebHostFixture fixture = new();
            protected override AspNetCoreCommunicationListener Sut => fixture.Sut;
            protected override void VerifyHostDisposed(Times times) => fixture.Host.Verify(_ => _.Dispose(), times);
        }

        public sealed class WithGenericHost : Abort
        {
            readonly GenericHostFixture fixture = new();
            protected override AspNetCoreCommunicationListener Sut => fixture.Sut;
            protected override void VerifyHostDisposed(Times times) => fixture.Host.Verify(_ => _.Dispose(), times);
        }
    }

    public abstract class CloseAsync
    {
        readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

        protected abstract AspNetCoreCommunicationListener Sut { get; }

        protected abstract void VerifyHostStopAsync(CancellationToken expected, Times times);

        protected abstract void VerifyHostDispose(Times times);

        [Fact]
        public async Task DoesNotThrowBeforeOpenAsync() =>
            await Sut.CloseAsync(cancellationToken);

        [Fact]
        public async Task PassesCancellationTokenToHostStopAsyncAfterOpenAsync()
        {
            _ = await Sut.OpenAsync(CancellationToken.None);

            await Sut.CloseAsync(cancellationToken);

            VerifyHostStopAsync(cancellationToken, Times.Once());
            VerifyHostDispose(Times.Once());
        }

        public sealed class WithWebHost : CloseAsync
        {
            readonly WebHostFixture fixture = new();
            protected override AspNetCoreCommunicationListener Sut => fixture.Sut;
            protected override void VerifyHostStopAsync(CancellationToken expected, Times times) =>
                fixture.Host.Verify(_ => _.StopAsync(expected), times);
            protected override void VerifyHostDispose(Times times) =>
                fixture.Host.Verify(_ => _.Dispose(), times);
        }

        public sealed class WithGenericHost : CloseAsync
        {
            readonly GenericHostFixture fixture = new();
            protected override AspNetCoreCommunicationListener Sut => fixture.Sut;
            protected override void VerifyHostStopAsync(CancellationToken expected, Times times) =>
                fixture.Host.Verify(_ => _.StopAsync(expected), times);
            protected override void VerifyHostDispose(Times times) =>
                fixture.Host.Verify(_ => _.Dispose(), times);
        }
    }

    public abstract class OpenAsync
    {
        readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

        protected abstract AspNetCoreCommunicationListener Sut { get; }

        protected abstract string CapturedBuildUrl { get; }

        protected abstract AspNetCoreCommunicationListener CapturedBuildListener { get; }

        protected abstract void VerifyHostStartAsync(CancellationToken expected, Times times);

        protected abstract AspNetCoreCommunicationListener CreateListener(ServiceContext context, string listenerUrl);

        [Fact]
        public async Task InvokesBuildDelegateWithGetListenerUrlAndSelf()
        {
            _ = await Sut.OpenAsync(cancellationToken);

            Assert.Equal(((TestListener)Sut).GetListenerUrl(), CapturedBuildUrl);
            Assert.Same(Sut, CapturedBuildListener);
        }

        [Fact]
        public async Task PassesCancellationTokenToHostStartAsync()
        {
            _ = await Sut.OpenAsync(cancellationToken);

            VerifyHostStartAsync(cancellationToken, Times.Once());
        }

        [Fact]
        public async Task ReturnsTaskThatCompletesWithPublishAddressAndUrlSuffix()
        {
            StatelessServiceContext context = fuzzy.StatelessServiceContext();
            AspNetCoreCommunicationListener listener = CreateListener(context, "http://+:0/");
            listener.ConfigureToUseUniqueServiceUrl();

            string url = await listener.OpenAsync(cancellationToken);

            string expected = string.Format(
                CultureInfo.InvariantCulture,
                "http://{0}:0/{1}/{2}",
                context.PublishAddress,
                context.PartitionId,
                context.ReplicaOrInstanceId);
            Assert.Equal(expected, url);
        }

        public sealed class WithWebHost : OpenAsync
        {
            readonly WebHostFixture fixture = new();
            protected override AspNetCoreCommunicationListener Sut => fixture.Sut;
            protected override string CapturedBuildUrl => fixture.BuildUrl;
            protected override AspNetCoreCommunicationListener CapturedBuildListener => fixture.BuildListener;
            protected override void VerifyHostStartAsync(CancellationToken expected, Times times) =>
                fixture.Host.Verify(_ => _.StartAsync(expected), times);
            protected override AspNetCoreCommunicationListener CreateListener(ServiceContext context, string listenerUrl) =>
                new WebHostFixture(context, listenerUrl).Sut;
        }

        public sealed class WithGenericHost : OpenAsync
        {
            readonly GenericHostFixture fixture = new();
            protected override AspNetCoreCommunicationListener Sut => fixture.Sut;
            protected override string CapturedBuildUrl => fixture.BuildUrl;
            protected override AspNetCoreCommunicationListener CapturedBuildListener => fixture.BuildListener;
            protected override void VerifyHostStartAsync(CancellationToken expected, Times times) =>
                fixture.Host.Verify(_ => _.StartAsync(expected), times);
            protected override AspNetCoreCommunicationListener CreateListener(ServiceContext context, string listenerUrl) =>
                new GenericHostFixture(context, listenerUrl).Sut;
        }
    }

    sealed class WebHostFixture
    {
        public Mock<IWebHost> Host { get; } = new();
        public TestListener Sut { get; }
        public string BuildUrl { get; private set; }
        public AspNetCoreCommunicationListener BuildListener { get; private set; }

        public WebHostFixture()
            : this(fuzzy.ServiceContext(), "http://+:0")
        {
        }

        public WebHostFixture(ServiceContext context, string listenerUrl)
        {
            _ = Host.Setup(_ => _.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = Host.Setup(_ => _.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
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
    }

    sealed class GenericHostFixture
    {
        public Mock<IHost> Host { get; } = new();
        public TestListener Sut { get; }
        public string BuildUrl { get; private set; }
        public AspNetCoreCommunicationListener BuildListener { get; private set; }

        public GenericHostFixture()
            : this(fuzzy.ServiceContext(), "http://+:0")
        {
        }

        public GenericHostFixture(ServiceContext context, string listenerUrl)
        {
            _ = Host.Setup(_ => _.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _ = Host.Setup(_ => _.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
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
