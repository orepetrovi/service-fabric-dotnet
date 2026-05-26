// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
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

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Non-null path calls FabricRuntime.GetActivationContext().
        public void AddsServiceFabricConfigurationSourceForEachConfigurationPackage()
        {
            // The non-null branch of AddServiceFabricConfiguration(IConfigurationBuilder) calls the static
            // FabricRuntime.GetActivationContext(), which requires a hosted Service Fabric runtime and cannot be
            // substituted in unit tests. Exercising this branch is not possible without refactoring the SUT to accept
            // an injectable factory for ICodePackageActivationContext. Fixing the underlying testability limitation is
            // out of scope for the current change.
            throw new NotImplementedException();
        }
    }

    public sealed class AddServiceFabricConfiguration_IConfigurationBuilder_ICodePackageActivationContext : ServiceFabricConfigurationExtensionsTest
    {
        readonly IConfigurationBuilder builder = new ConfigurationBuilder();
        readonly ICodePackageActivationContext context = new TestCodePackageActivationContext(new ConfigurationBuilder().Build());

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

            AssertSources(builder.Sources, multi, name1, name2);
        }

        [Fact]
        public void ReturnsBuilder()
        {
            IConfigurationBuilder actual = builder.AddServiceFabricConfiguration(context);
            Assert.Same(builder, actual);
        }

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
    }

    public sealed class AddServiceFabricConfiguration_IConfigurationBuilder_ICodePackageActivationContext_ActionOfServiceFabricConfigurationOptions : ServiceFabricConfigurationExtensionsTest
    {
        readonly IConfigurationBuilder builder = new ConfigurationBuilder();
        readonly ICodePackageActivationContext context = new TestCodePackageActivationContext(new ConfigurationBuilder().Build());
        readonly Action<ServiceFabricConfigurationOptions> optionsDelegate = Mock.Of<Action<ServiceFabricConfigurationOptions>>();

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

            AssertSources(builder.Sources, multi, name1, name2);
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
        public void AddsNoSourcesWhenContextHasNoConfigurationPackages()
        {
            var empty = new TestCodePackageActivationContext(new Dictionary<string, IConfiguration>());

            _ = builder.AddServiceFabricConfiguration(empty, optionsDelegate);

            Assert.Empty(builder.Sources);
            Mock.Get(optionsDelegate).Verify(_ => _(It.IsAny<ServiceFabricConfigurationOptions>()), Times.Never);
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

        [Fact]
        public void ReturnsBuilder()
        {
            IConfigurationBuilder actual = builder.AddServiceFabricConfiguration(context, optionsDelegate);
            Assert.Same(builder, actual);
        }

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
    }

    static void AssertSources(IList<IConfigurationSource> sources, ICodePackageActivationContext expectedContext, params string[] expectedPackageNames)
    {
        Assert.All(sources, source => Assert.Same(expectedContext, source.Property<ICodePackageActivationContext>().Value));
        var actual = sources.Select(source => source.Field<ServiceFabricConfigurationOptions>().Value.PackageName).ToList();
        Assert.Equal(expectedPackageNames.OrderBy(name => name), actual.OrderBy(name => name));
    }

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);
}
