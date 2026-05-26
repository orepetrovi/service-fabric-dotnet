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
    readonly string urlSuffix = "/" + fuzzy.String().LettersOrDigits();
    readonly ServiceFabricIntegrationOptions options = ServiceFabricIntegrationOptions.UseReverseProxyIntegration;

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ServiceFabricSetupFilterTest() =>
        sut = new ServiceFabricSetupFilter(urlSuffix, options);

    public sealed class Configure : ServiceFabricSetupFilterTest
    {
        // Method parameters
        readonly Mock<Action<IApplicationBuilder>> next = new();

        readonly Mock<IApplicationBuilder> app = new();
        readonly List<Func<RequestDelegate, RequestDelegate>> factories = new();

        public Configure()
        {
            _ = app.Setup(_ => _.ApplicationServices).Returns(Mock.Of<IServiceProvider>());
            _ = app.Setup(_ => _.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
                .Callback<Func<RequestDelegate, RequestDelegate>>(factories.Add)
                .Returns(app.Object);
        }

        IReadOnlyList<object> RegisteredMiddlewares()
        {
            RequestDelegate terminal = _ => Task.CompletedTask;
            return factories.Select(f => f(terminal).Target).ToList();
        }

        [Fact]
        public void ReturnsActionThatCallsNextWithApplicationBuilder()
        {
            Action<IApplicationBuilder> configured = sut.Configure(next.Object);
            configured(app.Object);

            next.Verify(_ => _(app.Object), Times.Once);
            next.Verify(_ => _(It.IsAny<IApplicationBuilder>()), Times.Once);
        }

        [Fact]
        public void ReturnsActionThatRegistersServiceFabricMiddlewareWithUrlSuffixWhenUrlSuffixIsNotEmpty()
        {
            sut.Configure(next.Object)(app.Object);

            ServiceFabricMiddleware middleware = RegisteredMiddlewares().OfType<ServiceFabricMiddleware>().Single();
            Assert.Equal(urlSuffix, middleware.Field<string>().Value);
        }

        [Fact]
        public void ReturnsActionThatDoesNotRegisterServiceFabricMiddlewareWhenUrlSuffixIsNull()
        {
            var filter = new ServiceFabricSetupFilter(null, options);
            filter.Configure(next.Object)(app.Object);
            Assert.Empty(RegisteredMiddlewares().OfType<ServiceFabricMiddleware>());
        }

        [Fact]
        public void ReturnsActionThatDoesNotRegisterServiceFabricMiddlewareWhenUrlSuffixIsEmpty()
        {
            var filter = new ServiceFabricSetupFilter(string.Empty, options);
            filter.Configure(next.Object)(app.Object);
            Assert.Empty(RegisteredMiddlewares().OfType<ServiceFabricMiddleware>());
        }

        [Theory]
        [InlineData(ServiceFabricIntegrationOptions.UseReverseProxyIntegration)]
        [InlineData(ServiceFabricIntegrationOptions.UseReverseProxyIntegration | ServiceFabricIntegrationOptions.UseUniqueServiceUrl)]
        public void ReturnsActionThatRegistersReverseProxyIntegrationMiddlewareWhenOptionsHasUseReverseProxyIntegration(ServiceFabricIntegrationOptions options)
        {
            var filter = new ServiceFabricSetupFilter(urlSuffix, options);
            filter.Configure(next.Object)(app.Object);
            Assert.Single(RegisteredMiddlewares().OfType<ServiceFabricReverseProxyIntegrationMiddleware>());
        }

        [Theory]
        [InlineData(ServiceFabricIntegrationOptions.None)]
        [InlineData(ServiceFabricIntegrationOptions.UseUniqueServiceUrl)]
        public void ReturnsActionThatDoesNotRegisterReverseProxyIntegrationMiddlewareWhenOptionsDoesNotHaveUseReverseProxyIntegration(ServiceFabricIntegrationOptions options)
        {
            var filter = new ServiceFabricSetupFilter(urlSuffix, options);
            filter.Configure(next.Object)(app.Object);
            Assert.Empty(RegisteredMiddlewares().OfType<ServiceFabricReverseProxyIntegrationMiddleware>());
        }

        [Fact]
        public void ReturnsActionThatRegistersServiceFabricMiddlewareBeforeReverseProxyIntegrationMiddleware()
        {
            sut.Configure(next.Object)(app.Object);

            Type[] middlewareTypes = RegisteredMiddlewares().Select(_ => _.GetType()).ToArray();
            Assert.Equal(new[] { typeof(ServiceFabricMiddleware), typeof(ServiceFabricReverseProxyIntegrationMiddleware) }, middlewareTypes);
        }

        [Fact]
        public void ReturnsActionThatRegistersMiddlewaresBeforeCallingNext()
        {
            int middlewareCountWhenNextCalled = -1;
            _ = next.Setup(_ => _(app.Object)).Callback(() => middlewareCountWhenNextCalled = factories.Count);

            sut.Configure(next.Object)(app.Object);

            Assert.Equal(2, middlewareCountWhenNextCalled);
        }
    }
}
