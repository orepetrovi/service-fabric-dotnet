// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using System.Fabric.Description;
using System.Globalization;
using System.Reflection;
using Fuzzy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.ServiceFabric.AspNetCore.Tests;
using Moq;
using Xunit;

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
            Assert.Equal("endpointName cannot be null or empty string.", exception.Message);
        }

        [Fact]
        public void ThrowsArgumentExceptionWhenEndpointNameIsEmpty()
        {
            var exception = Assert.Throws<ArgumentException>(() => new HttpSysCommunicationListener(serviceContext, string.Empty, build));
            Assert.Equal("endpointName cannot be null or empty string.", exception.Message);
        }
    }

    public sealed class Constructor_ServiceContext_String_FuncOfStringOfAspNetCoreCommunicationListenerOfIWebHost : HttpSysCommunicationListenerTest
    {
        [Fact]
        public void ThrowsArgumentExceptionWhenEndpointNameIsNull()
        {
            var exception = Assert.Throws<ArgumentException>(() => new HttpSysCommunicationListener(serviceContext, null, build));
            Assert.Equal("endpointName cannot be null or empty string.", exception.Message);
        }

        [Fact]
        public void ThrowsArgumentExceptionWhenEndpointNameIsEmpty()
        {
            var exception = Assert.Throws<ArgumentException>(() => new HttpSysCommunicationListener(serviceContext, string.Empty, build));
            Assert.Equal("endpointName cannot be null or empty string.", exception.Message);
        }
    }

    public sealed class GetListenerUrl : HttpSysCommunicationListenerTest
    {
        // TestMocksRepository wires an endpoint collection into the mocked ICodePackageActivationContext
        // that these tests mutate; fuzzy.StatelessServiceContext() does not provide that plumbing.
        readonly StatelessServiceContext context = TestMocksRepository.GetMockStatelessServiceContext();
        readonly TestListener listener;

        public GetListenerUrl() =>
            listener = new TestListener(context, endpointName, build);

        [Fact]
        public void ReturnsUrlWithProtocolLowercaseAndPortFromEndpoint()
        {
            var endpoint = new EndpointResourceDescription
            {
                Name = endpointName,
                Protocol = fuzzy.Enum<EndpointProtocol>(),
            };
            int port = fuzzy.Int32();
            typeof(EndpointResourceDescription).GetProperty(nameof(EndpointResourceDescription.Port)).SetValue(endpoint, port);
            context.CodePackageActivationContext.GetEndpoints().Add(endpoint);

            string actual = listener.GetListenerUrl();

            string expected = string.Format(CultureInfo.InvariantCulture, "{0}://+:{1}", endpoint.Protocol.ToString().ToLowerInvariant(), port);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ThrowsInvalidOperationExceptionWhenEndpointIsNotInManifest() =>
            _ = Assert.Throws<InvalidOperationException>(() => listener.GetListenerUrl());
    }

    sealed class TestListener : HttpSysCommunicationListener
    {
        internal TestListener(ServiceContext serviceContext, string endpointName, Func<string, AspNetCoreCommunicationListener, IWebHost> build)
            : base(serviceContext, endpointName, build)
        {
        }

        internal new string GetListenerUrl() =>
            base.GetListenerUrl();
    }
}
