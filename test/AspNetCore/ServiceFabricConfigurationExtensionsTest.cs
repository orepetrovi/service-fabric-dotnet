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

        [Fact]
        public void ThrowsArgumentNullExceptionWhenBuilderIsNull()
        {
            IConfigurationBuilder builder = null;
            var exception = Assert.Throws<ArgumentNullException>(
                () => ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration(builder));
            Assert.Equal(nameof(builder), exception.ParamName);
        }
    }

    public sealed class AddServiceFabricConfiguration_IConfigurationBuilder_ICodePackageActivationContext : ServiceFabricConfigurationExtensionsTest
    {
        readonly IConfigurationBuilder builder = new ConfigurationBuilder();
        readonly ICodePackageActivationContext context = new TestCodePackageActivationContext(new ConfigurationBuilder().Build());

        [Fact]
        public void AddsServiceFabricConfigurationSourceForEachConfigurationPackage()
        {
            TestCodePackageActivationContext multi = CreateMultiPackageContext(out string name1, out string name2);

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
            Assert.Equal(nameof(builder), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration(builder, null));
            Assert.Equal(nameof(context), exception.ParamName);
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
            TestCodePackageActivationContext multi = CreateMultiPackageContext(out string name1, out string name2);

            _ = builder.AddServiceFabricConfiguration(multi, optionsDelegate);

            AssertSources(builder.Sources, multi, name1, name2);
        }

        [Fact]
        public void InvokesOptionsDelegateForEachConfigurationPackage()
        {
            TestCodePackageActivationContext multi = CreateMultiPackageContext(out string name1, out string name2);

            var captured = new List<string>();
            _ = Mock.Get(optionsDelegate).Setup(_ => _(It.IsAny<ServiceFabricConfigurationOptions>()))
                .Callback((ServiceFabricConfigurationOptions o) => captured.Add(o.PackageName));

            _ = builder.AddServiceFabricConfiguration(multi, optionsDelegate);

            Assert.Equal(new[] { name1, name2 }.OrderBy(_ => _), captured.OrderBy(_ => _));
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
            var options = source.Field<ServiceFabricConfigurationOptions>().Value;
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
            Assert.Equal(nameof(builder), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => ServiceFabricConfigurationExtensions.AddServiceFabricConfiguration(builder, null, optionsDelegate));
            Assert.Equal(nameof(context), exception.ParamName);
        }
    }

    static void AssertSources(IList<IConfigurationSource> sources, ICodePackageActivationContext expectedContext, params string[] expectedPackageNames)
    {
        Assert.All(sources, source => Assert.Same(expectedContext, source.Property<ICodePackageActivationContext>().Value));
        IEnumerable<string> actual = sources.Select(source => source.Field<ServiceFabricConfigurationOptions>().Value.PackageName);
        Assert.Equal(expectedPackageNames.OrderBy(_ => _), actual.OrderBy(_ => _));
    }

    static TestCodePackageActivationContext CreateMultiPackageContext(out string name1, out string name2)
    {
        name1 = fuzzy.String();
        name2 = name1 + fuzzy.String();
        IConfiguration empty = new ConfigurationBuilder().Build();
        return new TestCodePackageActivationContext(new Dictionary<string, IConfiguration>
        {
            { name1, empty },
            { name2, empty },
        });
    }

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);
}
