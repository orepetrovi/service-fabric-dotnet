// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fuzzy;
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
    readonly string urlSuffix = "/" + fuzzy.String().LettersOrDigits();
    readonly ServiceFabricIntegrationOptions options = fuzzy.Enum<ServiceFabricIntegrationOptions>();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ServiceFabricSetupFilterTest() =>
        sut = new ServiceFabricSetupFilter(urlSuffix, options);

    public sealed class Configure : ServiceFabricSetupFilterTest
    {
        // Method parameters
        readonly Mock<Action<IApplicationBuilder>> next = new();

        readonly Mock<IApplicationBuilder> app = new();
        readonly List<Func<RequestDelegate, RequestDelegate>> factories = [];

        public Configure()
        {
            _ = app.Setup(_ => _.ApplicationServices).Returns(Mock.Of<IServiceProvider>());
            _ = app.Setup(_ => _.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
                .Callback<Func<RequestDelegate, RequestDelegate>>(factories.Add)
                .Returns(app.Object);
        }

        [Fact]
        public void ReturnsActionThatCallsNextWithApplicationBuilder()
        {
            Action<IApplicationBuilder> configured = sut.Configure(next.Object);
            configured(app.Object);

            next.Verify(_ => _(app.Object), Times.Once);
            next.Verify(_ => _(It.IsAny<IApplicationBuilder>()), Times.Once);
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
            var exception = Assert.Throws<ArgumentNullException>(() => sut.Configure(next.Object)(null));
            Assert.Equal(nameof(app), exception.ParamName);
        }

        [Theory]
        [InlineData(ServiceFabricIntegrationOptions.None)]
        [InlineData(ServiceFabricIntegrationOptions.UseUniqueServiceUrl)]
        [InlineData(ServiceFabricIntegrationOptions.UseReverseProxyIntegration)]
        [InlineData(ServiceFabricIntegrationOptions.UseReverseProxyIntegration | ServiceFabricIntegrationOptions.UseUniqueServiceUrl)]
        public async Task ReturnsActionThatRegistersServiceFabricMiddlewareWithUrlSuffixWhenUrlSuffixIsNotEmpty(ServiceFabricIntegrationOptions options)
        {
            var sut = new ServiceFabricSetupFilter(urlSuffix, options);
            sut.Configure(next.Object)(app.Object);

            // Verify SUT forwarded urlSuffix by exercising the registered middleware's observable behavior:
            // a request whose Path does not start with urlSuffix as a segment is rejected with 410 Gone.
            Func<RequestDelegate, RequestDelegate> factory = factories
                .Single(f => f(_ => Task.CompletedTask).Target is ServiceFabricMiddleware);
            RequestDelegate pipeline = factory(_ => Task.CompletedTask);
            var context = new DefaultHttpContext();
            context.Request.Path = urlSuffix + fuzzy.String().LettersOrDigits(); // appended without '/' so not a segment match
            await pipeline(context);
            Assert.Equal(StatusCodes.Status410Gone, context.Response.StatusCode);
        }

        [Theory, InlineData(null), InlineData("")]
        public void ReturnsActionThatDoesNotRegisterServiceFabricMiddlewareWhenUrlSuffixIsNullOrEmpty(string urlSuffix)
        {
            var sut = new ServiceFabricSetupFilter(urlSuffix, options);
            sut.Configure(next.Object)(app.Object);
            Assert.Empty(RegisteredMiddlewares().OfType<ServiceFabricMiddleware>());
        }

        [Theory, InlineData(ServiceFabricIntegrationOptions.UseReverseProxyIntegration)]
        [InlineData(ServiceFabricIntegrationOptions.UseReverseProxyIntegration | ServiceFabricIntegrationOptions.UseUniqueServiceUrl)]
        public void ReturnsActionThatRegistersReverseProxyIntegrationMiddlewareWhenOptionsHasUseReverseProxyIntegration(ServiceFabricIntegrationOptions options)
        {
            var sut = new ServiceFabricSetupFilter(urlSuffix, options);
            sut.Configure(next.Object)(app.Object);
            _ = Assert.Single(RegisteredMiddlewares().OfType<ServiceFabricReverseProxyIntegrationMiddleware>());
        }

        [Theory, InlineData(ServiceFabricIntegrationOptions.None), InlineData(ServiceFabricIntegrationOptions.UseUniqueServiceUrl)]
        public void ReturnsActionThatDoesNotRegisterReverseProxyIntegrationMiddlewareWhenOptionsDoesNotHaveUseReverseProxyIntegration(ServiceFabricIntegrationOptions options)
        {
            var sut = new ServiceFabricSetupFilter(urlSuffix, options);
            sut.Configure(next.Object)(app.Object);
            Assert.Empty(RegisteredMiddlewares().OfType<ServiceFabricReverseProxyIntegrationMiddleware>());
        }

        [Fact]
        public void ReturnsActionThatRegistersServiceFabricMiddlewareBeforeReverseProxyIntegrationMiddleware()
        {
            var sut = new ServiceFabricSetupFilter(urlSuffix, ServiceFabricIntegrationOptions.UseReverseProxyIntegration);
            sut.Configure(next.Object)(app.Object);

            Type[] middlewareTypes = [.. RegisteredMiddlewares().Select(_ => _.GetType())];
            Assert.Equal([typeof(ServiceFabricMiddleware), typeof(ServiceFabricReverseProxyIntegrationMiddleware)], middlewareTypes);
        }

        [Fact]
        public void ReturnsActionThatRegistersMiddlewaresBeforeCallingNext()
        {
            var sut = new ServiceFabricSetupFilter(urlSuffix, ServiceFabricIntegrationOptions.UseReverseProxyIntegration);
            List<int> middlewareCountsWhenNextCalled = [];
            _ = next.Setup(_ => _(app.Object)).Callback(() => middlewareCountsWhenNextCalled.Add(factories.Count));

            sut.Configure(next.Object)(app.Object);

            Assert.Equal([factories.Count], middlewareCountsWhenNextCalled);
            next.Verify(_ => _(It.IsAny<IApplicationBuilder>()), Times.Once);
        }

        IReadOnlyList<object> RegisteredMiddlewares() =>
            [.. factories.Select(f => f(_ => Task.CompletedTask).Target)];
    }
}
