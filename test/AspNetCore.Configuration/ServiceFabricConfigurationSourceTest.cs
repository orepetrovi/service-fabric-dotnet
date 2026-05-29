// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Fabric;
using Fuzzy;
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
        public void ReturnsConfigurationProviderInitializedFromSource()
        {
            options.ConfigAction = (_, _) => { };
            IConfigurationProvider provider = sut.Build(builder);
            provider.Load();
            Mock.Get(activationContext).Verify(_ => _.GetConfigurationPackageObject(options.PackageName), Times.Once);
        }
    }

    public sealed class Constructor : ServiceFabricConfigurationSourceTest
    {
        [Fact]
        public void InitializesActivationContext() =>
            Assert.Same(activationContext, ((ServiceFabricConfigurationSource)sut).ActivationContext);
    }
}
