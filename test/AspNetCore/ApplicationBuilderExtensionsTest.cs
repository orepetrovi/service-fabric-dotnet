// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Fuzzy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.AspNetCore;

public abstract class ApplicationBuilderExtensionsTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    readonly Mock<IApplicationBuilder> builder = new();

    protected ApplicationBuilderExtensionsTest() =>
        _ = builder.Setup(_ => _.ApplicationServices).Returns(Mock.Of<IServiceProvider>());

    public sealed class UseServiceFabricMiddleware : ApplicationBuilderExtensionsTest
    {
        readonly string urlSuffix = "/" + fuzzy.String().LettersOrDigits();

        [Fact]
        public async Task ReturnsApplicationBuilderAfterRegisteringMiddleware()
        {
            Func<RequestDelegate, RequestDelegate> factory = null;
            _ = builder
                .Setup(_ => _.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
                .Callback<Func<RequestDelegate, RequestDelegate>>(f => factory = f)
                .Returns(builder.Object);

            IApplicationBuilder actual = builder.Object.UseServiceFabricMiddleware(urlSuffix);

            Assert.Same(builder.Object, actual);
            Assert.NotNull(factory);

            // Invoke the captured factory to construct the real middleware delegate and prove
            // ServiceFabricMiddleware was registered with urlSuffix: when the request Path does not
            // start with urlSuffix the middleware short-circuits with 410 Gone and does not call next.
            bool nextCalled = false;
            RequestDelegate middleware = factory(_ => { nextCalled = true; return Task.CompletedTask; });
            var context = new DefaultHttpContext();
            context.Request.Path = "/different-" + fuzzy.String().LettersOrDigits();

            await middleware(context);

            Assert.False(nextCalled);
            Assert.Equal(StatusCodes.Status410Gone, context.Response.StatusCode);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuilderIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => ApplicationBuilderExtensions.UseServiceFabricMiddleware(null, urlSuffix));
            Assert.Equal(nameof(builder), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenUrlSuffixIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => builder.Object.UseServiceFabricMiddleware(null));
            Assert.Equal(nameof(urlSuffix), exception.ParamName);
        }
    }

    public sealed class UseServiceFabricReverseProxyIntegrationMiddleware : ApplicationBuilderExtensionsTest
    {
        [Fact]
        public async Task ReturnsApplicationBuilderAfterRegisteringMiddleware()
        {
            Func<RequestDelegate, RequestDelegate> factory = null;
            _ = builder
                .Setup(_ => _.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
                .Callback<Func<RequestDelegate, RequestDelegate>>(f => factory = f)
                .Returns(builder.Object);

            IApplicationBuilder actual = builder.Object.UseServiceFabricReverseProxyIntegrationMiddleware();

            Assert.Same(builder.Object, actual);
            Assert.NotNull(factory);

            // Invoke the captured factory to construct the real middleware delegate and prove
            // ServiceFabricReverseProxyIntegrationMiddleware was registered: it registers an OnStarting
            // callback that adds the X-ServiceFabric: ResourceNotFound header when the status is 404.
            // DefaultHttpContext's response feature does not fire OnStarting callbacks, so capture and
            // invoke the registered callback directly via a custom IHttpResponseFeature.
            var responseFeature = new CapturingResponseFeature();
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature());
            features.Set<IHttpResponseFeature>(responseFeature);
            var context = new DefaultHttpContext(features);

            bool nextCalled = false;
            RequestDelegate middleware = factory(ctx =>
            {
                nextCalled = true;
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            });

            await middleware(context);

            Assert.True(nextCalled);
            Assert.NotNull(responseFeature.OnStartingCallback);
            await responseFeature.OnStartingCallback(responseFeature.OnStartingState);
            Assert.Equal("ResourceNotFound", context.Response.Headers["X-ServiceFabric"].ToString());
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuilderIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => ApplicationBuilderExtensions.UseServiceFabricReverseProxyIntegrationMiddleware(null));
            Assert.Equal(nameof(builder), exception.ParamName);
        }

        sealed class CapturingResponseFeature : HttpResponseFeature
        {
            public Func<object, Task> OnStartingCallback { get; private set; }

            public object OnStartingState { get; private set; }

            public override void OnStarting(Func<object, Task> callback, object state)
            {
                OnStartingCallback = callback;
                OnStartingState = state;
            }
        }
    }
}
