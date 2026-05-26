// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Threading.Tasks;
using Fuzzy;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.AspNetCore;

public abstract class ServiceFabricMiddlewareTest
{
    readonly ServiceFabricMiddleware sut;

    readonly RequestDelegate next = Mock.Of<RequestDelegate>(_ => _(It.IsAny<HttpContext>()) == Task.CompletedTask);
    readonly string urlSuffix = "/" + fuzzy.String().LettersOrDigits();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ServiceFabricMiddlewareTest() =>
        sut = new ServiceFabricMiddleware(next, urlSuffix);

    public sealed class Constructor : ServiceFabricMiddlewareTest
    {
        [Fact]
        public void ThrowsArgumentNullExceptionWhenNextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new ServiceFabricMiddleware(null, urlSuffix));
            Assert.Equal(nameof(next), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenUrlSuffixIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new ServiceFabricMiddleware(next, null));
            Assert.Equal(nameof(urlSuffix), exception.ParamName);
        }
    }

    public sealed class Invoke : ServiceFabricMiddlewareTest
    {
        // Method parameters
        readonly HttpContext context = new DefaultHttpContext();

        [Fact]
        public async Task ThrowsArgumentNullExceptionWhenContextIsNull()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => sut.Invoke(null));
            Assert.Equal(nameof(context), exception.ParamName);
        }

        [Fact]
        public async Task CallsNextWhenUrlSuffixIsEmpty()
        {
            var middleware = new ServiceFabricMiddleware(next, string.Empty);
            await middleware.Invoke(context);
            Mock.Get(next).Verify(_ => _(context), Times.Once);
        }

        [Fact]
        public async Task SetsStatusCodeGoneWhenPathDoesNotStartWithUrlSuffix()
        {
            context.Request.Path = urlSuffix + fuzzy.String().LettersOrDigits(); // appended without '/' so not a segment match

            await sut.Invoke(context);

            Assert.Equal(StatusCodes.Status410Gone, context.Response.StatusCode);
            Mock.Get(next).Verify(_ => _(It.IsAny<HttpContext>()), Times.Never);
        }

        [Fact]
        public async Task CallsNextWithRemainingPathAndExtendedPathBaseWhenPathStartsWithUrlSuffix()
        {
            // Arrange
            string remainingPath = "/" + fuzzy.String().LettersOrDigits();
            string originalPathBase = "/" + fuzzy.String().LettersOrDigits();
            context.Request.PathBase = originalPathBase;
            context.Request.Path = urlSuffix + remainingPath;

            PathString actualPath = default;
            PathString actualPathBase = default;
            Mock.Get(next)
                .Setup(_ => _(context))
                .Callback<HttpContext>(c => { actualPath = c.Request.Path; actualPathBase = c.Request.PathBase; })
                .Returns(Task.CompletedTask);

            // Act
            await sut.Invoke(context);

            // Assert
            Assert.Equal(remainingPath, actualPath.Value);
            Assert.Equal(originalPathBase + urlSuffix, actualPathBase.Value);
        }

        [Fact]
        public async Task RestoresPathAndPathBaseAfterCallingNext()
        {
            string remainingPath = "/" + fuzzy.String().LettersOrDigits();
            PathString originalPathBase = "/" + fuzzy.String().LettersOrDigits();
            PathString originalPath = urlSuffix + remainingPath;
            context.Request.PathBase = originalPathBase;
            context.Request.Path = originalPath;

            await sut.Invoke(context);

            Assert.Equal(originalPath, context.Request.Path);
            Assert.Equal(originalPathBase, context.Request.PathBase);
        }

        [Fact]
        public async Task RestoresPathAndPathBaseWhenNextThrows()
        {
            // Arrange
            string remainingPath = "/" + fuzzy.String().LettersOrDigits();
            PathString originalPathBase = "/" + fuzzy.String().LettersOrDigits();
            PathString originalPath = urlSuffix + remainingPath;
            context.Request.PathBase = originalPathBase;
            context.Request.Path = originalPath;

            var expected = new InvalidOperationException();
            Mock.Get(next).Setup(_ => _(context)).ThrowsAsync(expected);

            // Act
            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Invoke(context));

            // Assert
            Assert.Same(expected, actual);
            Assert.Equal(originalPath, context.Request.Path);
            Assert.Equal(originalPathBase, context.Request.PathBase);
        }
    }
}
