// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using Fuzzy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.AspNetCore;

public abstract class ApplicationBuilderExtensionsTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    readonly Mock<IApplicationBuilder> builder = new();

    public sealed class UseServiceFabricMiddleware : ApplicationBuilderExtensionsTest
    {
        readonly string urlSuffix = fuzzy.String();

        [Fact]
        public void ReturnsApplicationBuilderAfterRegisteringMiddleware()
        {
            _ = builder.Setup(_ => _.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>())).Returns(builder.Object);

            IApplicationBuilder actual = builder.Object.UseServiceFabricMiddleware(urlSuffix);

            Assert.Same(builder.Object, actual);
            builder.Verify(_ => _.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()), Times.Once);
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
        public void ReturnsApplicationBuilderAfterRegisteringMiddleware()
        {
            _ = builder.Setup(_ => _.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>())).Returns(builder.Object);

            IApplicationBuilder actual = builder.Object.UseServiceFabricReverseProxyIntegrationMiddleware();

            Assert.Same(builder.Object, actual);
            builder.Verify(_ => _.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()), Times.Once);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuilderIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => ApplicationBuilderExtensions.UseServiceFabricReverseProxyIntegrationMiddleware(null));
            Assert.Equal(nameof(builder), exception.ParamName);
        }
    }
}
