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

        [Fact(Explicit = true)] // TODO: SUT bug. Missing paramName argument to ArgumentException.
        public void SetsParamNameToEndpointNameOnArgumentException()
        {
            // The constructor throws `new ArgumentException(SR.EndpointNameNullOrEmptyExceptionMessage)` without
            // passing the paramName argument, so the resulting exception's ParamName is null and gives callers no
            // indication which argument was invalid. This test asserts the correct ParamName and will fail until the
            // SUT is fixed. Fixing the SUT is out of scope for the current change.
            var exception = Assert.Throws<ArgumentException>(() => new HttpSysCommunicationListener(serviceContext, null, build));
            Assert.Equal(
                sut.Constructor<Action<ServiceContext, string, Func<string, AspNetCoreCommunicationListener, IHost>>>().Parameter<string>().Name,
                exception.ParamName);
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

        [Fact(Explicit = true)] // TODO: SUT bug. Missing paramName argument to ArgumentException.
        public void SetsParamNameToEndpointNameOnArgumentException()
        {
            // The constructor throws `new ArgumentException(SR.EndpointNameNullOrEmptyExceptionMessage)` without
            // passing the paramName argument, so the resulting exception's ParamName is null and gives callers no
            // indication which argument was invalid. This test asserts the correct ParamName and will fail until the
            // SUT is fixed. Fixing the SUT is out of scope for the current change.
            var exception = Assert.Throws<ArgumentException>(() => new HttpSysCommunicationListener(serviceContext, null, build));
            Assert.Equal(
                sut.Constructor<Action<ServiceContext, string, Func<string, AspNetCoreCommunicationListener, IWebHost>>>().Parameter<string>().Name,
                exception.ParamName);
        }
    }

    public abstract class GetListenerUrl : HttpSysCommunicationListenerTest
    {
        // TestMocksRepository wires an endpoint collection into the mocked ICodePackageActivationContext
        // that these tests mutate; fuzzy.StatelessServiceContext() does not provide that plumbing.
        readonly StatelessServiceContext context = TestMocksRepository.GetMockStatelessServiceContext();
        protected new readonly AspNetCoreCommunicationListener sut;

        protected GetListenerUrl(Func<ServiceContext, string, HttpSysCommunicationListener> create) =>
            sut = create(context, endpointName);

        public sealed class WithIHost : GetListenerUrl
        {
            public WithIHost()
                : base((c, n) => new HttpSysCommunicationListener(c, n, (_, _) => Mock.Of<IHost>())) { }
        }

        public sealed class WithIWebHost : GetListenerUrl
        {
            public WithIWebHost()
                : base((c, n) => new HttpSysCommunicationListener(c, n, (_, _) => Mock.Of<IWebHost>())) { }
        }

        [Theory]
        [InlineData(EndpointProtocol.Tcp, "tcp")]
        [InlineData(EndpointProtocol.Http, "http")]
        [InlineData(EndpointProtocol.Https, "https")]
        [InlineData(EndpointProtocol.Udp, "udp")]
        public void ReturnsUrlWithProtocolLowercaseAndPortFromEndpoint(EndpointProtocol protocol, string expectedScheme)
        {
            var endpoint = new EndpointResourceDescription
            {
                Name = endpointName,
                Protocol = protocol,
            };
            int port = fuzzy.UInt16();
            endpoint.Property<int>().Set(port);
            context.CodePackageActivationContext.GetEndpoints().Add(endpoint);

            string actual = sut.GetListenerUrl();

            string expected = $"{expectedScheme}://+:{port}";
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ReturnsUrlForEndpointMatchingNameWhenMultipleEndpointsExist()
        {
            var other = new EndpointResourceDescription
            {
                Name = endpointName + fuzzy.String(),
                Protocol = EndpointProtocol.Https,
            };
            other.Property<int>().Set(fuzzy.UInt16());
            context.CodePackageActivationContext.GetEndpoints().Add(other);

            var endpoint = new EndpointResourceDescription
            {
                Name = endpointName,
                Protocol = EndpointProtocol.Http,
            };
            int port = fuzzy.UInt16();
            endpoint.Property<int>().Set(port);
            context.CodePackageActivationContext.GetEndpoints().Add(endpoint);

            string actual = sut.GetListenerUrl();

            string expected = $"http://+:{port}";
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ThrowsInvalidOperationExceptionWhenEndpointIsNotInManifest()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => sut.GetListenerUrl());
            Assert.Equal(string.Format(CultureInfo.InvariantCulture, AspNetCoreSR.EndpointNameNotFoundExceptionMessage, endpointName), exception.Message);
        }
    }
}
