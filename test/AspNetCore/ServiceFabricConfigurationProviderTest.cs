// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric;
using Fuzzy;
using Microsoft.Extensions.Configuration;
using Microsoft.ServiceFabric.AspNetCore.Tests;
using Xunit;

namespace Microsoft.ServiceFabric.AspNetCore.Configuration;

public abstract class ServiceFabricConfigurationProviderTest
{
    readonly ServiceFabricConfigurationProvider sut;

    // Constructor parameters
    readonly TestCodePackageActivationContext activationContext = new(new ConfigurationBuilder().Build());
    readonly ServiceFabricConfigurationOptions options = new("Config");

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ServiceFabricConfigurationProviderTest() =>
        sut = new ServiceFabricConfigurationProvider(activationContext, options);

    public sealed class ConfigAction : ServiceFabricConfigurationProviderTest
    {
        readonly string section = fuzzy.String().LettersOrDigits();
        readonly string key1 = fuzzy.String().LettersOrDigits();
        readonly string key2;
        readonly string val1 = fuzzy.String().LettersOrDigits();
        readonly string val2 = fuzzy.String().LettersOrDigits();

        readonly TestCodePackageActivationContext context;
        readonly IConfiguration config;
        readonly List<(int Sections, int Values)> invocations = new();

        public ConfigAction()
        {
            key2 = key1 + fuzzy.String().LettersOrDigits();

            var contextConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{section}:{key1}", val1 },
                { $"{section}:{key2}", val2 },
            }).Build();

            context = new TestCodePackageActivationContext(contextConfig);

            var builder = new ConfigurationBuilder();
            builder.AddServiceFabricConfiguration(context, options =>
            {
                options.ConfigAction = (package, configData) =>
                {
                    int sections = 0;
                    int values = 0;
                    foreach (var section in package.Settings.Sections)
                    {
                        sections++;
                        foreach (var param in section.Parameters)
                        {
                            configData[options.ExtractKeyFunc(section, param)] = options.ExtractValueFunc(section, param);
                            values++;
                        }
                    }
                    invocations.Add((sections, values));
                };
                options.IncludePackageName = false;
            });

            config = builder.Build();
        }

        [Fact]
        public void IsInvokedOnInitialLoad()
        {
            Assert.Equal((1, 2), Assert.Single(invocations));
            Assert.Equal(val1, config[$"{section}:{key1}"]);
            Assert.Equal(val2, config[$"{section}:{key2}"]);
        }

        [Fact]
        public void IsInvokedOnConfigurationPackageModifiedEvent()
        {
            string val1Updated = val1 + fuzzy.String().LettersOrDigits();

            context.TriggerConfigurationPackageModifiedEvent(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                {
                    { $"{section}:{key1}", val1Updated },
                }).Build(),
                "Config");

            Assert.Equal((1, 1), invocations[^1]);
            Assert.Equal(val1Updated, config[$"{section}:{key1}"]);
            Assert.Null(config[$"{section}:{key2}"]);
        }
    }

    public sealed class ConfigurationPackageAddedEvent : ServiceFabricConfigurationProviderTest
    {
        [Fact]
        public void ReloadsConfigurationWhenPackageNameMatches()
        {
            string section = fuzzy.String().LettersOrDigits();
            string key = fuzzy.String().LettersOrDigits();
            string initial = fuzzy.String().LettersOrDigits();
            string updated = initial + fuzzy.String().LettersOrDigits();

            var contextConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{section}:{key}", initial },
            }).Build();

            var context = new TestCodePackageActivationContext(contextConfig);

            var builder = new ConfigurationBuilder();
            builder.AddServiceFabricConfiguration(context);
            var config = builder.Build();

            Assert.Equal(initial, config[$"Config:{section}:{key}"]);

            var reloaded = false;
            config.GetReloadToken().RegisterChangeCallback(_ => reloaded = true, null);

            var addedPackage = MockConfigurationPackage.CreateDefaultPackage(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                {
                    { $"{section}:{key}", updated },
                }).Build(),
                "Config");

            context.RaiseConfigurationPackageAddedEvent(addedPackage);

            Assert.Equal(updated, config[$"Config:{section}:{key}"]);
            Assert.True(reloaded);
        }
    }

    public sealed class ConfigurationPackageModifiedEvent : ServiceFabricConfigurationProviderTest
    {
        [Fact]
        public void ReloadsConfigurationWhenPackageNameMatches()
        {
            string section = fuzzy.String().LettersOrDigits();
            string key1 = fuzzy.String().LettersOrDigits();
            string key2 = key1 + fuzzy.String().LettersOrDigits();
            string key3 = key2 + fuzzy.String().LettersOrDigits();
            string val1a = fuzzy.String().LettersOrDigits();
            string val2a = fuzzy.String().LettersOrDigits();
            string val3 = fuzzy.String().LettersOrDigits();
            string val1b = val1a + fuzzy.String().LettersOrDigits();
            string val2b = val2a + fuzzy.String().LettersOrDigits();

            var contextConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{section}:{key1}", val1a },
                { $"{section}:{key2}", val2a },
                { $"{section}:{key3}", val3 },
            }).Build();

            var context = new TestCodePackageActivationContext(contextConfig);

            var builder = new ConfigurationBuilder();
            builder.AddServiceFabricConfiguration(context);
            var config = builder.Build();

            Assert.Equal(val1a, config[$"Config:{section}:{key1}"]);
            Assert.Equal(val2a, config[$"Config:{section}:{key2}"]);
            Assert.Equal(val3, config[$"Config:{section}:{key3}"]);

            var reloadToken = config.GetReloadToken();
            Assert.False(reloadToken.HasChanged);

            context.TriggerConfigurationPackageModifiedEvent(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                {
                    { $"{section}:{key1}", val1b },
                    { $"{section}:{key2}", val2b },
                    { $"{section}:{key3}", val3 },
                }).Build(),
                "Config");

            Assert.True(reloadToken.HasChanged, "Expected configuration reload token to fire after package update.");
            Assert.Equal(val1b, config[$"Config:{section}:{key1}"]);
            Assert.Equal(val2b, config[$"Config:{section}:{key2}"]);
            Assert.Equal(val3, config[$"Config:{section}:{key3}"]);
        }

        [Fact]
        public void LoadsConfigurationFromMultiplePackages()
        {
            string sharedSection = fuzzy.String().LettersOrDigits();
            string section1 = fuzzy.String().LettersOrDigits();
            string section2 = section1 + fuzzy.String().LettersOrDigits();
            string sharedKey = fuzzy.String().LettersOrDigits();
            string key1a = fuzzy.String().LettersOrDigits();
            string key1b = key1a + fuzzy.String().LettersOrDigits();
            string key2a = fuzzy.String().LettersOrDigits();
            string key2b = key2a + fuzzy.String().LettersOrDigits();
            string shared1 = fuzzy.String().LettersOrDigits();
            string val1a = fuzzy.String().LettersOrDigits();
            string val1b = fuzzy.String().LettersOrDigits();
            string shared2 = shared1 + fuzzy.String().LettersOrDigits();
            string val2a = fuzzy.String().LettersOrDigits();
            string val2b = fuzzy.String().LettersOrDigits();

            var contextConfig1 = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{sharedSection}:{sharedKey}", shared1 },
                { $"{section1}:{key1a}", val1a },
                { $"{section1}:{key1b}", val1b },
            }).Build();

            var contextConfig2 = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{sharedSection}:{sharedKey}", shared2 },
                { $"{section2}:{key2a}", val2a },
                { $"{section2}:{key2b}", val2b },
            }).Build();

            var context = new TestCodePackageActivationContext(new Dictionary<string, IConfiguration>() { { "Config1", contextConfig1 }, { "Config2", contextConfig2 } });

            var builder = new ConfigurationBuilder();
            builder.AddServiceFabricConfiguration(context);
            var config = builder.Build();

            Assert.Equal(shared1, config[$"Config1:{sharedSection}:{sharedKey}"]);
            Assert.Equal(val1a, config[$"Config1:{section1}:{key1a}"]);
            Assert.Equal(val1b, config[$"Config1:{section1}:{key1b}"]);

            Assert.Equal(shared2, config[$"Config2:{sharedSection}:{sharedKey}"]);
            Assert.Equal(val2a, config[$"Config2:{section2}:{key2a}"]);
            Assert.Equal(val2b, config[$"Config2:{section2}:{key2b}"]);
        }

        [Fact]
        public void OnlyReloadsMappedConfigPackage()
        {
            string sharedSection = fuzzy.String().LettersOrDigits();
            string section1 = fuzzy.String().LettersOrDigits();
            string section2 = section1 + fuzzy.String().LettersOrDigits();
            string sharedKey = fuzzy.String().LettersOrDigits();
            string key1a = fuzzy.String().LettersOrDigits();
            string key1b = key1a + fuzzy.String().LettersOrDigits();
            string key2a = fuzzy.String().LettersOrDigits();
            string key2b = key2a + fuzzy.String().LettersOrDigits();
            string shared1 = fuzzy.String().LettersOrDigits();
            string val1a = fuzzy.String().LettersOrDigits();
            string val1b = fuzzy.String().LettersOrDigits();
            string shared2 = shared1 + fuzzy.String().LettersOrDigits();
            string val2a = fuzzy.String().LettersOrDigits();
            string val2b = fuzzy.String().LettersOrDigits();
            string sharedReloaded = shared2 + fuzzy.String().LettersOrDigits();
            string val1aReloaded = val1a + fuzzy.String().LettersOrDigits();
            string val1bReloaded = val1b + fuzzy.String().LettersOrDigits();

            var contextConfig1 = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{sharedSection}:{sharedKey}", shared1 },
                { $"{section1}:{key1a}", val1a },
                { $"{section1}:{key1b}", val1b },
            }).Build();

            var contextConfig2 = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{sharedSection}:{sharedKey}", shared2 },
                { $"{section2}:{key2a}", val2a },
                { $"{section2}:{key2b}", val2b },
            }).Build();

            var context = new TestCodePackageActivationContext(new Dictionary<string, IConfiguration>() { { "Config1", contextConfig1 }, { "Config2", contextConfig2 } });

            var builder = new ConfigurationBuilder();
            builder.AddServiceFabricConfiguration(context);
            var config = builder.Build() as ConfigurationRoot;

            context.TriggerConfigurationPackageModifiedEvent(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                {
                    { $"{sharedSection}:{sharedKey}", sharedReloaded },
                    { $"{section1}:{key1a}", val1aReloaded },
                    { $"{section1}:{key1b}", val1bReloaded },
                }).Build(),
                "Config1");

            Assert.Equal(sharedReloaded, config[$"Config1:{sharedSection}:{sharedKey}"]);
            Assert.Equal(val1aReloaded, config[$"Config1:{section1}:{key1a}"]);
            Assert.Equal(val1bReloaded, config[$"Config1:{section1}:{key1b}"]);

            Assert.Equal(shared2, config[$"Config2:{sharedSection}:{sharedKey}"]);
            Assert.Equal(val2a, config[$"Config2:{section2}:{key2a}"]);
            Assert.Equal(val2b, config[$"Config2:{section2}:{key2b}"]);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenPackageDescriptionIsNull()
        {
            var contextConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{fuzzy.String().LettersOrDigits()}:{fuzzy.String().LettersOrDigits()}", fuzzy.String().LettersOrDigits() },
            }).Build();

            var context = new TestCodePackageActivationContext(contextConfig);

            var builder = new ConfigurationBuilder();
            builder.AddServiceFabricConfiguration(context);
            builder.Build();

            var packageWithoutDescription = TestHelper.CreateInstanced<ConfigurationPackage>();

            var exception = Assert.Throws<ArgumentNullException>(
                () => context.RaiseConfigurationPackageModifiedEvent(packageWithoutDescription));
            Assert.Equal("package.Description", exception.ParamName);
        }

        [Fact]
        public void IgnoresPackageWhenNameDoesNotMatch()
        {
            string section = fuzzy.String().LettersOrDigits();
            string key = fuzzy.String().LettersOrDigits();
            string initial = fuzzy.String().LettersOrDigits();
            string ignored = initial + fuzzy.String().LettersOrDigits();

            var contextConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{section}:{key}", initial },
            }).Build();

            var context = new TestCodePackageActivationContext(contextConfig);

            var builder = new ConfigurationBuilder();
            builder.AddServiceFabricConfiguration(context);
            var config = builder.Build();

            Assert.Equal(initial, config[$"Config:{section}:{key}"]);

            var reloaded = false;
            config.GetReloadToken().RegisterChangeCallback(_ => reloaded = true, null);

            var otherPackage = MockConfigurationPackage.CreateDefaultPackage(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                {
                    { $"{section}:{key}", ignored },
                }).Build(),
                "OtherPackage");

            context.RaiseConfigurationPackageModifiedEvent(otherPackage);

            Assert.Equal(initial, config[$"Config:{section}:{key}"]);
            Assert.False(reloaded);
        }
    }

    public sealed class Constructor : ServiceFabricConfigurationProviderTest
    {
        [Fact]
        public void ThrowsArgumentNullExceptionWhenOptionsIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ServiceFabricConfigurationProvider(activationContext, null));
            Assert.Equal(nameof(options), exception.ParamName);
        }
    }

    public sealed class Load : ServiceFabricConfigurationProviderTest
    {
        [Fact]
        public void PrefixesKeysWithPackageNameByDefault()
        {
            string section = fuzzy.String().LettersOrDigits();
            string key = fuzzy.String().LettersOrDigits();
            string value = fuzzy.String().LettersOrDigits();

            var contextConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{section}:{key}", value },
            }).Build();

            var context = new TestCodePackageActivationContext(contextConfig);

            var builder = new ConfigurationBuilder();
            builder.AddServiceFabricConfiguration(context);
            var config = builder.Build();

            Assert.Equal(value, config[$"Config:{section}:{key}"]);
            Assert.Null(config[$"{section}:{key}"]);
        }

        [Fact]
        public void ReturnsNullForUnknownKey()
        {
            string section = fuzzy.String().LettersOrDigits();
            string knownKey = fuzzy.String().LettersOrDigits();
            string unknownKey = knownKey + fuzzy.String().LettersOrDigits();

            var contextConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{section}:{knownKey}", fuzzy.String().LettersOrDigits() },
            }).Build();

            var context = new TestCodePackageActivationContext(contextConfig);

            var builder = new ConfigurationBuilder();
            builder.AddServiceFabricConfiguration(context);
            var config = builder.Build();

            Assert.Null(config[$"Config:{section}:{unknownKey}"]);
        }

        [Fact]
        public void LoadsEmptyConfiguration()
        {
            var contextConfig = new ConfigurationBuilder().Build();
            var context = new TestCodePackageActivationContext(contextConfig);

            var builder = new ConfigurationBuilder();
            builder.AddServiceFabricConfiguration(context);
            var config = builder.Build();

            Assert.Empty(config.GetChildren());
        }
    }

    internal class Person
    {
        public string Name { get; set; }

        public string Gender { get; set; }

        public int Age { get; set; }
    }
}
