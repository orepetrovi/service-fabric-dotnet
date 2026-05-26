// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric;
using Fuzzy;
using Inspector;
using Microsoft.Extensions.Configuration;
using Microsoft.ServiceFabric.AspNetCore.Tests;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.AspNetCore.Configuration;

public abstract class ServiceFabricConfigurationExtensionsTest
{
    public sealed class AddServiceFabricConfiguration_IConfigurationBuilder : ServiceFabricConfigurationExtensionsTest
    {
        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuilderIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration(null));
            string expected = typeof(ServiceFabricConfigurationExtensions)
                .Method<Func<IConfigurationBuilder, IConfigurationBuilder>>(nameof(ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration))
                .Parameter<IConfigurationBuilder>().Name;
            Assert.Equal(expected, exception.ParamName);
        }
    }

    public sealed class AddServiceFabricConfiguration_IConfigurationBuilder_ICodePackageActivationContext : ServiceFabricConfigurationExtensionsTest
    {
        // Method parameters
        readonly IConfigurationBuilder builder = new ConfigurationBuilder();
        readonly ICodePackageActivationContext context = new TestCodePackageActivationContext(new ConfigurationBuilder().Build());

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuilderIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration(null, context));
            string expected = typeof(ServiceFabricConfigurationExtensions)
                .Method<Func<IConfigurationBuilder, ICodePackageActivationContext, IConfigurationBuilder>>(nameof(ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration))
                .Parameter<IConfigurationBuilder>().Name;
            Assert.Equal(expected, exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration(builder, null));
            string expected = typeof(ServiceFabricConfigurationExtensions)
                .Method<Func<IConfigurationBuilder, ICodePackageActivationContext, IConfigurationBuilder>>(nameof(ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration))
                .Parameter<ICodePackageActivationContext>().Name;
            Assert.Equal(expected, exception.ParamName);
        }

        [Fact]
        public void ReturnsBuilder()
        {
            IConfigurationBuilder actual = builder.AddServiceFabricConfiguration(context);
            Assert.Same(builder, actual);
        }

        [Fact]
        public void AddsServiceFabricConfigurationSourceForEachConfigurationPackage()
        {
            string name1 = fuzzy.String();
            string name2 = fuzzy.String();
            IConfiguration empty = new ConfigurationBuilder().Build();
            var multi = new TestCodePackageActivationContext(new Dictionary<string, IConfiguration>
            {
                { name1, empty },
                { name2, empty },
            });

            _ = builder.AddServiceFabricConfiguration(multi);

            Assert.Collection(
                builder.Sources,
                source => AssertSource(source, multi, name1),
                source => AssertSource(source, multi, name2));
        }
    }

    public sealed class AddServiceFabricConfiguration_IConfigurationBuilder_ICodePackageActivationContext_ActionOfServiceFabricConfigurationOptions : ServiceFabricConfigurationExtensionsTest
    {
        // Method parameters
        readonly IConfigurationBuilder builder = new ConfigurationBuilder();
        readonly ICodePackageActivationContext context = new TestCodePackageActivationContext(new ConfigurationBuilder().Build());
        readonly Action<ServiceFabricConfigurationOptions> optionsDelegate = Mock.Of<Action<ServiceFabricConfigurationOptions>>();

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuilderIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration(null, context, optionsDelegate));
            string expected = typeof(ServiceFabricConfigurationExtensions)
                .Method<Func<IConfigurationBuilder, ICodePackageActivationContext, Action<ServiceFabricConfigurationOptions>, IConfigurationBuilder>>(nameof(ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration))
                .Parameter<IConfigurationBuilder>().Name;
            Assert.Equal(expected, exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration(builder, null, optionsDelegate));
            string expected = typeof(ServiceFabricConfigurationExtensions)
                .Method<Func<IConfigurationBuilder, ICodePackageActivationContext, Action<ServiceFabricConfigurationOptions>, IConfigurationBuilder>>(nameof(ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration))
                .Parameter<ICodePackageActivationContext>().Name;
            Assert.Equal(expected, exception.ParamName);
        }

        [Fact]
        public void ReturnsBuilder()
        {
            IConfigurationBuilder actual = builder.AddServiceFabricConfiguration(context, optionsDelegate);
            Assert.Same(builder, actual);
        }

        [Fact]
        public void AddsServiceFabricConfigurationSourceForEachConfigurationPackage()
        {
            string name1 = fuzzy.String();
            string name2 = fuzzy.String();
            IConfiguration empty = new ConfigurationBuilder().Build();
            var multi = new TestCodePackageActivationContext(new Dictionary<string, IConfiguration>
            {
                { name1, empty },
                { name2, empty },
            });

            _ = builder.AddServiceFabricConfiguration(multi, optionsDelegate);

            Assert.Collection(
                builder.Sources,
                source => AssertSource(source, multi, name1),
                source => AssertSource(source, multi, name2));
        }

        [Fact]
        public void InvokesOptionsDelegateForEachConfigurationPackage()
        {
            string name1 = fuzzy.String();
            string name2 = fuzzy.String();
            IConfiguration empty = new ConfigurationBuilder().Build();
            var multi = new TestCodePackageActivationContext(new Dictionary<string, IConfiguration>
            {
                { name1, empty },
                { name2, empty },
            });

            _ = builder.AddServiceFabricConfiguration(multi, optionsDelegate);

            Mock.Get(optionsDelegate).Verify(_ => _(It.Is<ServiceFabricConfigurationOptions>(o => o.PackageName == name1)), Times.Once);
            Mock.Get(optionsDelegate).Verify(_ => _(It.Is<ServiceFabricConfigurationOptions>(o => o.PackageName == name2)), Times.Once);
            Mock.Get(optionsDelegate).Verify(_ => _(It.IsAny<ServiceFabricConfigurationOptions>()), Times.Exactly(2));
        }

        [Fact]
        public void AppliesOptionsDelegateChangesToAddedSource()
        {
            Action<ServiceFabricConfigurationOptions> mutate = options => options.IncludePackageName = false;

            _ = builder.AddServiceFabricConfiguration(context, mutate);

            IConfigurationSource source = Assert.Single(builder.Sources);
            ServiceFabricConfigurationOptions options = source.Field<ServiceFabricConfigurationOptions>().Value;
            Assert.False(options.IncludePackageName);
        }

        [Fact]
        public void AddsSourceWhenOptionsDelegateIsNull()
        {
            _ = builder.AddServiceFabricConfiguration(context, optionsDelegate: null);

            IConfigurationSource source = Assert.Single(builder.Sources);
            Assert.Same(context, source.Property<ICodePackageActivationContext>().Value);
        }
    }

    static void AssertSource(IConfigurationSource source, ICodePackageActivationContext expectedContext, string expectedPackageName)
    {
        Assert.Same(expectedContext, source.Property<ICodePackageActivationContext>().Value);
        ServiceFabricConfigurationOptions options = source.Field<ServiceFabricConfigurationOptions>().Value;
        Assert.Equal(expectedPackageName, options.PackageName);
    }

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);
}
