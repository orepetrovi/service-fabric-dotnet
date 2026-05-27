// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using System.Fabric.Description;
using System.Globalization;
using Fuzzy;
using Inspector;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.ServiceFabric.AspNetCore.Tests;
using Moq;
using Xunit;
using AspNetCoreSR = Microsoft.ServiceFabric.Services.Communication.AspNetCore.SR;
using HttpSysSR = Microsoft.ServiceFabric.AspNetCore.HttpSys.SR;

namespace Microsoft.ServiceFabric.Services.Communication.AspNetCore;

public abstract class HttpSysCommunicationListenerTest
{
    readonly HttpSysCommunicationListener sut;

    // Constructor parameters
    readonly ServiceContext serviceContext = fuzzy.ServiceContext();
    readonly string endpointName = fuzzy.String();
    readonly Func<string, AspNetCoreCommunicationListener, IWebHost> build = (_, _) => Mock.Of<IWebHost>();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    HttpSysCommunicationListenerTest() =>
        sut = new HttpSysCommunicationListener(serviceContext, endpointName, build);

    public sealed class Constructor_ServiceContext_String_FuncOfStringOfAspNetCoreCommunicationListenerOfIHost : HttpSysCommunicationListenerTest
    {
        new readonly Func<string, AspNetCoreCommunicationListener, IHost> build = (_, _) => Mock.Of<IHost>();

        [Fact]
        public void ThrowsArgumentExceptionWhenEndpointNameIsNull()
        {
            var exception = Assert.Throws<ArgumentException>(() => new HttpSysCommunicationListener(serviceContext, null, build));
            Assert.Equal(HttpSysSR.EndpointNameNullOrEmptyExceptionMessage, exception.Message);
        }

        [Fact]
        public void ThrowsArgumentExceptionWhenEndpointNameIsEmpty()
        {
            var exception = Assert.Throws<ArgumentException>(() => new HttpSysCommunicationListener(serviceContext, string.Empty, build));
            Assert.Equal(HttpSysSR.EndpointNameNullOrEmptyExceptionMessage, exception.Message);
        }
    }

    public sealed class Constructor_ServiceContext_String_FuncOfStringOfAspNetCoreCommunicationListenerOfIWebHost : HttpSysCommunicationListenerTest
    {
        [Fact]
        public void ThrowsArgumentExceptionWhenEndpointNameIsNull()
        {
            var exception = Assert.Throws<ArgumentException>(() => new HttpSysCommunicationListener(serviceContext, null, build));
            Assert.Equal(HttpSysSR.EndpointNameNullOrEmptyExceptionMessage, exception.Message);
        }

        [Fact]
        public void ThrowsArgumentExceptionWhenEndpointNameIsEmpty()
        {
            var exception = Assert.Throws<ArgumentException>(() => new HttpSysCommunicationListener(serviceContext, string.Empty, build));
            Assert.Equal(HttpSysSR.EndpointNameNullOrEmptyExceptionMessage, exception.Message);
        }
    }

    public abstract class GetListenerUrl : HttpSysCommunicationListenerTest
    {
        // TestMocksRepository wires an endpoint collection into the mocked ICodePackageActivationContext
        // that these tests mutate; fuzzy.StatelessServiceContext() does not provide that plumbing.
        readonly StatelessServiceContext context = TestMocksRepository.GetMockStatelessServiceContext();
        readonly Func<string> getListenerUrl;

        protected GetListenerUrl(Func<ServiceContext, string, HttpSysCommunicationListener> create) =>
            getListenerUrl = create(context, endpointName).Protected().Method<Func<string>>();

        public sealed class WithIWebHost : GetListenerUrl
        {
            public WithIWebHost()
                : base((c, n) => new HttpSysCommunicationListener(c, n, (_, _) => Mock.Of<IWebHost>())) { }
        }

        public sealed class WithIHost : GetListenerUrl
        {
            public WithIHost()
                : base((c, n) => new HttpSysCommunicationListener(c, n, (_, _) => Mock.Of<IHost>())) { }
        }

        [Theory]
        [InlineData(EndpointProtocol.Tcp, "tcp")]
        [InlineData(EndpointProtocol.Http, "http")]
        [InlineData(EndpointProtocol.Https, "https")]
        [InlineData(EndpointProtocol.Udp, "udp")]
        public void ReturnsUrlWithProtocolLowercaseAndPortFromEndpoint(EndpointProtocol protocol, string expectedScheme)
        {
            var other = new EndpointResourceDescription
            {
                Name = endpointName + fuzzy.String(),
                Protocol = protocol == EndpointProtocol.Http ? EndpointProtocol.Https : EndpointProtocol.Http,
            };
            other.Property<int>().Set(fuzzy.UInt16());
            context.CodePackageActivationContext.GetEndpoints().Add(other);

            var endpoint = new EndpointResourceDescription
            {
                Name = endpointName,
                Protocol = protocol,
            };
            int port = fuzzy.UInt16();
            endpoint.Property<int>().Set(port);
            context.CodePackageActivationContext.GetEndpoints().Add(endpoint);

            string actual = getListenerUrl();

            string expected = string.Format(CultureInfo.InvariantCulture, "{0}://+:{1}", expectedScheme, port);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ThrowsInvalidOperationExceptionWhenEndpointIsNotInManifest()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => getListenerUrl());
            Assert.Equal(string.Format(CultureInfo.InvariantCulture, AspNetCoreSR.EndpointNameNotFoundExceptionMessage, endpointName), exception.Message);
        }
    }
}
