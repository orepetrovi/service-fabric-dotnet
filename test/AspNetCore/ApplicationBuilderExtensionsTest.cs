// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Moq;
using Xunit;
using ApplicationBuilderExtensions = Microsoft.ServiceFabric.Services.Communication.AspNetCore.ApplicationBuilderExtensions;

namespace Microsoft.ServiceFabric.AspNetCore.Tests;

public abstract class ApplicationBuilderExtensionsTest
{
    readonly Mock<IApplicationBuilder> builder = new();

    public sealed class UseServiceFabricMiddleware : ApplicationBuilderExtensionsTest
    {
        readonly string urlSuffix = Guid.NewGuid().ToString("N");

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
            Assert.Equal("builder", exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenUrlSuffixIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => builder.Object.UseServiceFabricMiddleware(null));
            Assert.Equal("urlSuffix", exception.ParamName);
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
            Assert.Equal("builder", exception.ParamName);
        }
    }
}
