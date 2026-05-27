// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

extern alias KestrelAssembly;

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
using KestrelAssembly::Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using AspNetCoreSR = Microsoft.ServiceFabric.Services.Communication.AspNetCore.SR;
using KestrelSR = KestrelAssembly::Microsoft.ServiceFabric.Services.Communication.AspNetCore.SR;

namespace Microsoft.ServiceFabric.Services.Communication.AspNetCore;

public abstract class KestrelCommunicationListenerTest
{
    readonly KestrelCommunicationListener sut;

    // Constructor parameters
    readonly ServiceContext serviceContext = fuzzy.ServiceContext();
    readonly string endpointName = fuzzy.String();
    readonly Func<string, AspNetCoreCommunicationListener, IWebHost> build = (_, _) => Mock.Of<IWebHost>();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    KestrelCommunicationListenerTest() =>
        sut = new KestrelCommunicationListener(serviceContext, endpointName, build);

    public sealed class Constructor_ServiceContext_String_FuncOfStringOfAspNetCoreCommunicationListenerOfIHost : KestrelCommunicationListenerTest
    {
        new readonly Func<string, AspNetCoreCommunicationListener, IHost> build = (_, _) => Mock.Of<IHost>();

        [Fact]
        public void ThrowsArgumentExceptionWhenEndpointNameIsEmpty()
        {
            var exception = Assert.Throws<ArgumentException>(() => new KestrelCommunicationListener(serviceContext, string.Empty, build));
            Assert.StartsWith(KestrelSR.EndpointNameEmptyExceptionMessage, exception.Message);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing paramName argument to ArgumentException.
        public void SetsParamNameToEndpointNameOnArgumentException()
        {
            // The constructor throws `new ArgumentException(SR.EndpointNameEmptyExceptionMessage)` without
            // passing the paramName argument, so the resulting exception's ParamName is null and gives callers no
            // indication which argument was invalid. This test asserts the correct ParamName and will fail until the
            // SUT is fixed. Fixing the SUT is out of scope for the current change.
            var exception = Assert.Throws<ArgumentException>(() => new KestrelCommunicationListener(serviceContext, string.Empty, build));
            Assert.Equal(nameof(endpointName), exception.ParamName);
        }
    }

    public sealed class Constructor_ServiceContext_String_FuncOfStringOfAspNetCoreCommunicationListenerOfIWebHost : KestrelCommunicationListenerTest
    {
        [Fact]
        public void ThrowsArgumentExceptionWhenEndpointNameIsEmpty()
        {
            var exception = Assert.Throws<ArgumentException>(() => new KestrelCommunicationListener(serviceContext, string.Empty, build));
            Assert.StartsWith(KestrelSR.EndpointNameEmptyExceptionMessage, exception.Message);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing paramName argument to ArgumentException.
        public void SetsParamNameToEndpointNameOnArgumentException()
        {
            // The constructor throws `new ArgumentException(SR.EndpointNameEmptyExceptionMessage)` without
            // passing the paramName argument, so the resulting exception's ParamName is null and gives callers no
            // indication which argument was invalid. This test asserts the correct ParamName and will fail until the
            // SUT is fixed. Fixing the SUT is out of scope for the current change.
            var exception = Assert.Throws<ArgumentException>(() => new KestrelCommunicationListener(serviceContext, string.Empty, build));
            Assert.Equal(nameof(endpointName), exception.ParamName);
        }
    }

    public abstract class GetListenerUrl : KestrelCommunicationListenerTest
    {
        // TestMocksRepository wires an endpoint collection into the mocked ICodePackageActivationContext
        // that these tests mutate; fuzzy.StatelessServiceContext() does not provide that plumbing.
        readonly StatelessServiceContext context = TestMocksRepository.GetMockStatelessServiceContext();
        new readonly AspNetCoreCommunicationListener sut;

        GetListenerUrl(Func<ServiceContext, string, KestrelCommunicationListener> create) =>
            sut = create(context, endpointName);

        public sealed class WithIHost : GetListenerUrl
        {
            public WithIHost()
                : base((c, n) => new KestrelCommunicationListener(c, n, (_, _) => Mock.Of<IHost>())) { }
        }

        public sealed class WithIWebHost : GetListenerUrl
        {
            public WithIWebHost()
                : base((c, n) => new KestrelCommunicationListener(c, n, (_, _) => Mock.Of<IWebHost>())) { }
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

    public abstract class GetListenerUrl_WithoutEndpointName : KestrelCommunicationListenerTest
    {
        readonly StatelessServiceContext context = TestMocksRepository.GetMockStatelessServiceContext();
        new readonly AspNetCoreCommunicationListener sut;

        GetListenerUrl_WithoutEndpointName(Func<ServiceContext, AspNetCoreCommunicationListener> create) =>
            sut = create(context);

        public sealed class WithIHost : GetListenerUrl_WithoutEndpointName
        {
            public WithIHost()
                : base(c => new KestrelCommunicationListener(c, (_, _) => Mock.Of<IHost>())) { }
        }

        public sealed class WithIWebHost : GetListenerUrl_WithoutEndpointName
        {
            public WithIWebHost()
                : base(c => new KestrelCommunicationListener(c, (_, _) => Mock.Of<IWebHost>())) { }
        }

        public sealed class WithNullEndpointName : GetListenerUrl_WithoutEndpointName
        {
            public WithNullEndpointName()
                : base(c => new KestrelCommunicationListener(c, null, (_, _) => Mock.Of<IWebHost>())) { }
        }

        [Fact]
        public void ReturnsDefaultHttpUrlOnPortZero()
        {
            string actual = sut.GetListenerUrl();
            Assert.Equal("http://+:0", actual);
        }
    }
}
