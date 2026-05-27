// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

extern alias KestrelAssembly;

using System;
using System.Fabric;
using System.Fabric.Description;
using System.Globalization;
using System.Reflection;
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

    public sealed class Constructor_ServiceContext_FuncOfStringOfAspNetCoreCommunicationListenerOfIHost : KestrelCommunicationListenerTest
    {
        // TODO: Inspector v0.9.0 sut.Constructor<TSig>() binds multiple overloads when delegate-typed parameters
        // only differ in generic arguments (relaxed signature matching). Track via olegsych/inspector once filed.
        static readonly ConstructorInfo ctor = typeof(KestrelCommunicationListener).GetConstructor(new[] { typeof(ServiceContext), typeof(Func<string, AspNetCoreCommunicationListener, IHost>) })!;

        new readonly Func<string, AspNetCoreCommunicationListener, IHost> build = (_, _) => Mock.Of<IHost>();

        [Fact]
        public void ThrowsArgumentNullExceptionWhenServiceContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new KestrelCommunicationListener(null, build));
            Assert.Equal(ctor.Parameter<ServiceContext>().Name, exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuildIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new KestrelCommunicationListener(serviceContext, (Func<string, AspNetCoreCommunicationListener, IHost>)null));
            Assert.Equal(ctor.Parameter<Func<string, AspNetCoreCommunicationListener, IHost>>().Name, exception.ParamName);
        }
    }

    public sealed class Constructor_ServiceContext_FuncOfStringOfAspNetCoreCommunicationListenerOfIWebHost : KestrelCommunicationListenerTest
    {
        static readonly ConstructorInfo ctor = typeof(KestrelCommunicationListener).GetConstructor(new[] { typeof(ServiceContext), typeof(Func<string, AspNetCoreCommunicationListener, IWebHost>) })!;

        [Fact]
        public void ThrowsArgumentNullExceptionWhenServiceContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new KestrelCommunicationListener(null, build));
            Assert.Equal(ctor.Parameter<ServiceContext>().Name, exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuildIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new KestrelCommunicationListener(serviceContext, (Func<string, AspNetCoreCommunicationListener, IWebHost>)null));
            Assert.Equal(ctor.Parameter<Func<string, AspNetCoreCommunicationListener, IWebHost>>().Name, exception.ParamName);
        }
    }

    public sealed class Constructor_ServiceContext_String_FuncOfStringOfAspNetCoreCommunicationListenerOfIHost : KestrelCommunicationListenerTest
    {
        static readonly ConstructorInfo ctor = typeof(KestrelCommunicationListener).GetConstructor(new[] { typeof(ServiceContext), typeof(string), typeof(Func<string, AspNetCoreCommunicationListener, IHost>) })!;

        new readonly Func<string, AspNetCoreCommunicationListener, IHost> build = (_, _) => Mock.Of<IHost>();

        [Fact]
        public void ThrowsArgumentNullExceptionWhenServiceContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new KestrelCommunicationListener(null, endpointName, build));
            Assert.Equal(ctor.Parameter<ServiceContext>().Name, exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuildIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new KestrelCommunicationListener(serviceContext, endpointName, (Func<string, AspNetCoreCommunicationListener, IHost>)null));
            Assert.Equal(ctor.Parameter<Func<string, AspNetCoreCommunicationListener, IHost>>().Name, exception.ParamName);
        }

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
            Assert.Equal(ctor.Parameter<string>().Name, exception.ParamName);
        }
    }

    public sealed class Constructor_ServiceContext_String_FuncOfStringOfAspNetCoreCommunicationListenerOfIWebHost : KestrelCommunicationListenerTest
    {
        static readonly ConstructorInfo ctor = typeof(KestrelCommunicationListener).GetConstructor(new[] { typeof(ServiceContext), typeof(string), typeof(Func<string, AspNetCoreCommunicationListener, IWebHost>) })!;

        [Fact]
        public void ThrowsArgumentNullExceptionWhenServiceContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new KestrelCommunicationListener(null, endpointName, build));
            Assert.Equal(ctor.Parameter<ServiceContext>().Name, exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuildIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new KestrelCommunicationListener(serviceContext, endpointName, (Func<string, AspNetCoreCommunicationListener, IWebHost>)null));
            Assert.Equal(ctor.Parameter<Func<string, AspNetCoreCommunicationListener, IWebHost>>().Name, exception.ParamName);
        }

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
            Assert.Equal(ctor.Parameter<string>().Name, exception.ParamName);
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
            // Minimum(1) avoids port 0: for the Http row, port 0 would make `expected` byte-identical
            // to the literal returned by KestrelCommunicationListener.GetListenerUrl's no-endpoint
            // default path ("http://+:0"), defeating this test's discrimination of the endpoint branch.
            int port = fuzzy.UInt16().Minimum(1);
            endpoint.Property<int>().Set(port);
            context.CodePackageActivationContext.GetEndpoints().Add(endpoint);

            string actual = sut.GetListenerUrl();

            string expected = $"{expectedScheme}://+:{port}";
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ReturnsUrlForEndpointMatchingNameWhenMultipleEndpointsExist()
        {
            var endpoint = new EndpointResourceDescription
            {
                Name = endpointName,
                Protocol = EndpointProtocol.Http,
            };
            int port = fuzzy.UInt16();
            endpoint.Property<int>().Set(port);
            context.CodePackageActivationContext.GetEndpoints().Add(endpoint);

            var other = new EndpointResourceDescription
            {
                Name = endpointName + fuzzy.String(),
                Protocol = EndpointProtocol.Https,
            };
            other.Property<int>().Set(fuzzy.UInt16());
            context.CodePackageActivationContext.GetEndpoints().Add(other);

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

        public abstract class WithoutEndpointName : KestrelCommunicationListenerTest
        {
            readonly StatelessServiceContext context = TestMocksRepository.GetMockStatelessServiceContext();
            new readonly AspNetCoreCommunicationListener sut;

            WithoutEndpointName(Func<ServiceContext, AspNetCoreCommunicationListener> create) =>
                sut = create(context);

            public sealed class WithIHost : WithoutEndpointName
            {
                public WithIHost()
                    : base(c => new KestrelCommunicationListener(c, (_, _) => Mock.Of<IHost>())) { }
            }

            public sealed class WithIWebHost : WithoutEndpointName
            {
                public WithIWebHost()
                    : base(c => new KestrelCommunicationListener(c, (_, _) => Mock.Of<IWebHost>())) { }
            }

            [Fact]
            public void ReturnsDefaultHttpUrlOnPortZero()
            {
                string actual = sut.GetListenerUrl();
                Assert.Equal("http://+:0", actual);
            }
        }

        public abstract class WithNullEndpointName : KestrelCommunicationListenerTest
        {
            readonly StatelessServiceContext context = TestMocksRepository.GetMockStatelessServiceContext();
            new readonly AspNetCoreCommunicationListener sut;

            WithNullEndpointName(Func<ServiceContext, AspNetCoreCommunicationListener> create) =>
                sut = create(context);

            public sealed class WithIHost : WithNullEndpointName
            {
                public WithIHost()
                    : base(c => new KestrelCommunicationListener(c, null, (_, _) => Mock.Of<IHost>())) { }
            }

            public sealed class WithIWebHost : WithNullEndpointName
            {
                public WithIWebHost()
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
}
