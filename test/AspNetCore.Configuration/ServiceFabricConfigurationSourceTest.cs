// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Fabric;
using Fuzzy;
using Inspector;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.AspNetCore.Configuration;

public abstract class ServiceFabricConfigurationSourceTest
{
    readonly IConfigurationSource sut;

    // Constructor parameters
    readonly ICodePackageActivationContext activationContext = Mock.Of<ICodePackageActivationContext>();
    readonly ServiceFabricConfigurationOptions options = new(fuzzy.String());

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ServiceFabricConfigurationSourceTest() =>
        sut = new ServiceFabricConfigurationSource(activationContext, options);

    public sealed class Build : ServiceFabricConfigurationSourceTest
    {
        readonly IConfigurationBuilder builder = Mock.Of<IConfigurationBuilder>();

        [Fact]
        public void ReturnsServiceFabricConfigurationProviderInitializedFromSource()
        {
            var provider = (ServiceFabricConfigurationProvider)sut.Build(builder);
            Assert.Same(activationContext, provider.Field<ICodePackageActivationContext>().Value);
            Assert.Same(options, provider.Field<ServiceFabricConfigurationOptions>().Value);
        }
    }

    public sealed class Constructor : ServiceFabricConfigurationSourceTest
    {
        [Fact]
        public void InitializesActivationContext() =>
            Assert.Same(activationContext, ((ServiceFabricConfigurationSource)sut).ActivationContext);

        [Fact(Explicit = true)] // TODO: SUT bug. Constructor doesn't validate activationContext.
        public void ThrowsArgumentNullExceptionWhenActivationContextIsNull()
        {
            // The constructor stores activationContext without a null check. The value is then passed to
            // ServiceFabricConfigurationProvider in Build, where dereferencing it produces a NullReferenceException
            // instead of an ArgumentNullException naming the offending parameter.
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ServiceFabricConfigurationSource(null, options));
            Assert.Equal(nameof(activationContext), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Constructor doesn't validate options.
        public void ThrowsArgumentNullExceptionWhenOptionsIsNull()
        {
            // The constructor stores options without a null check. ServiceFabricConfigurationProvider throws
            // ArgumentNullException for its own options parameter when Build is called, but the source itself
            // should fail fast and name its own parameter.
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ServiceFabricConfigurationSource(activationContext, null));
            Assert.Equal(nameof(options), exception.ParamName);
        }
    }
}
