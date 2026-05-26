// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fuzzy;
using Inspector;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.AspNetCore;

public abstract class ServiceFabricSetupFilterTest
{
    readonly IStartupFilter sut;

    // Constructor parameters
    readonly string urlSuffix = fuzzy.String();
    readonly ServiceFabricIntegrationOptions options = fuzzy.Enum<ServiceFabricIntegrationOptions>();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ServiceFabricSetupFilterTest() =>
        sut = new ServiceFabricSetupFilter(urlSuffix, options);

    public sealed class Configure : ServiceFabricSetupFilterTest
    {
        // Method parameters
        readonly Action<IApplicationBuilder> next = Mock.Of<Action<IApplicationBuilder>>();

        readonly Mock<IApplicationBuilder> app = new();
        readonly List<Func<RequestDelegate, RequestDelegate>> factories = new();

        public Configure()
        {
            _ = app.Setup(_ => _.ApplicationServices).Returns(Mock.Of<IServiceProvider>());
            _ = app.Setup(_ => _.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
                .Callback<Func<RequestDelegate, RequestDelegate>>(factories.Add)
                .Returns(app.Object);
        }

        IReadOnlyList<object> RegisteredMiddlewares() =>
            factories.Select(f => f(_ => Task.CompletedTask).Target).ToList();

        [Fact]
        public void ReturnsActionThatCallsNextWithApplicationBuilder()
        {
            Action<IApplicationBuilder> configured = sut.Configure(next);
            configured(app.Object);

            Mock.Get(next).Verify(_ => _(app.Object), Times.Once);
            Mock.Get(next).Verify(_ => _(It.IsAny<IApplicationBuilder>()), Times.Once);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing arg null validation.
        public void ThrowsArgumentNullExceptionWhenNextIsNull()
        {
            // SUT returns an action without validation; it throws NullReferenceException when the action
            // dereferences `next`. Expected behavior is to fail fast in Configure.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.Configure(null));
            Assert.Equal(nameof(next), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing arg null validation.
        public void ReturnsActionThatThrowsArgumentNullExceptionWhenAppIsNull()
        {
            // SUT's returned action does not validate `app`. Depending on branch it dereferences `app`
            // (NullReferenceException) or forwards null to `next` silently. Expected behavior is to fail fast.
            var sut = new ServiceFabricSetupFilter(null, ServiceFabricIntegrationOptions.None);
            var exception = Assert.Throws<ArgumentNullException>(() => sut.Configure(next)(null));
            Assert.Equal("app", exception.ParamName);
        }

        [Fact]
        public void ReturnsActionThatRegistersServiceFabricMiddlewareWithUrlSuffixWhenUrlSuffixIsNotEmpty()
        {
            sut.Configure(next)(app.Object);

            ServiceFabricMiddleware middleware = RegisteredMiddlewares().OfType<ServiceFabricMiddleware>().Single();
            Assert.Equal(urlSuffix, middleware.Field<string>().Value);
        }

        [Fact]
        public void ReturnsActionThatDoesNotRegisterServiceFabricMiddlewareWhenUrlSuffixIsNull()
        {
            var sut = new ServiceFabricSetupFilter(null, options);
            sut.Configure(next)(app.Object);
            Assert.Empty(RegisteredMiddlewares().OfType<ServiceFabricMiddleware>());
        }

        [Fact]
        public void ReturnsActionThatDoesNotRegisterServiceFabricMiddlewareWhenUrlSuffixIsEmpty()
        {
            var sut = new ServiceFabricSetupFilter(string.Empty, options);
            sut.Configure(next)(app.Object);
            Assert.Empty(RegisteredMiddlewares().OfType<ServiceFabricMiddleware>());
        }

        [Fact]
        public void ReturnsActionThatRegistersReverseProxyIntegrationMiddlewareWhenUrlSuffixIsNull()
        {
            var sut = new ServiceFabricSetupFilter(null, ServiceFabricIntegrationOptions.UseReverseProxyIntegration);
            sut.Configure(next)(app.Object);
            Assert.Single(RegisteredMiddlewares().OfType<ServiceFabricReverseProxyIntegrationMiddleware>());
        }

        [Fact]
        public void ReturnsActionThatRegistersReverseProxyIntegrationMiddlewareWhenUrlSuffixIsEmpty()
        {
            var sut = new ServiceFabricSetupFilter(string.Empty, ServiceFabricIntegrationOptions.UseReverseProxyIntegration);
            sut.Configure(next)(app.Object);
            Assert.Single(RegisteredMiddlewares().OfType<ServiceFabricReverseProxyIntegrationMiddleware>());
        }

        [Theory, InlineData(ServiceFabricIntegrationOptions.UseReverseProxyIntegration)]
        [InlineData(ServiceFabricIntegrationOptions.UseReverseProxyIntegration | ServiceFabricIntegrationOptions.UseUniqueServiceUrl)]
        public void ReturnsActionThatRegistersReverseProxyIntegrationMiddlewareWhenOptionsHasUseReverseProxyIntegration(ServiceFabricIntegrationOptions options)
        {
            var sut = new ServiceFabricSetupFilter(urlSuffix, options);
            sut.Configure(next)(app.Object);
            Assert.Single(RegisteredMiddlewares().OfType<ServiceFabricReverseProxyIntegrationMiddleware>());
        }

        [Theory, InlineData(ServiceFabricIntegrationOptions.None), InlineData(ServiceFabricIntegrationOptions.UseUniqueServiceUrl)]
        public void ReturnsActionThatDoesNotRegisterReverseProxyIntegrationMiddlewareWhenOptionsDoesNotHaveUseReverseProxyIntegration(ServiceFabricIntegrationOptions options)
        {
            var sut = new ServiceFabricSetupFilter(urlSuffix, options);
            sut.Configure(next)(app.Object);
            Assert.Empty(RegisteredMiddlewares().OfType<ServiceFabricReverseProxyIntegrationMiddleware>());
        }

        [Fact]
        public void ReturnsActionThatRegistersServiceFabricMiddlewareBeforeReverseProxyIntegrationMiddleware()
        {
            var sut = new ServiceFabricSetupFilter(urlSuffix, ServiceFabricIntegrationOptions.UseReverseProxyIntegration);
            sut.Configure(next)(app.Object);

            Type[] middlewareTypes = RegisteredMiddlewares().Select(_ => _.GetType()).ToArray();
            Assert.Equal(new[] { typeof(ServiceFabricMiddleware), typeof(ServiceFabricReverseProxyIntegrationMiddleware) }, middlewareTypes);
        }

        [Fact]
        public void ReturnsActionThatRegistersMiddlewaresBeforeCallingNext()
        {
            var sut = new ServiceFabricSetupFilter(urlSuffix, ServiceFabricIntegrationOptions.UseReverseProxyIntegration);
            int middlewareCountWhenNextCalled = -1;
            _ = Mock.Get(next).Setup(_ => _(app.Object)).Callback(() => middlewareCountWhenNextCalled = factories.Count);

            sut.Configure(next)(app.Object);

            Assert.Equal(factories.Count, middlewareCountWhenNextCalled);
        }
    }
}
