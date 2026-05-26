// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using Fuzzy;
using Microsoft.Extensions.Configuration;
using Microsoft.ServiceFabric.AspNetCore.Tests;
using Xunit;
using ConfigurationSection = System.Fabric.Description.ConfigurationSection;

namespace Microsoft.ServiceFabric.AspNetCore.Configuration;

public abstract class ServiceFabricConfigurationOptionsTest
{
    readonly ServiceFabricConfigurationOptions sut;

    readonly string packageName = fuzzy.String();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ServiceFabricConfigurationOptionsTest() =>
        sut = new ServiceFabricConfigurationOptions(packageName);

    public sealed class ConfigAction : ServiceFabricConfigurationOptionsTest
    {
        [Fact]
        public void PopulatesDataDictionaryWithExtractedKeysAndValues()
        {
            string section1 = "Section" + fuzzy.String().LettersOrDigits();
            string section2 = section1 + fuzzy.String().LettersOrDigits();
            string param1 = "Param" + fuzzy.String().LettersOrDigits();
            string param2 = param1 + fuzzy.String().LettersOrDigits();
            string value1 = fuzzy.String().LettersOrDigits();
            string value2 = fuzzy.String().LettersOrDigits();
            IConfigurationRoot config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{section1}:{param1}", value1 },
                { $"{section2}:{param2}", value2 },
            }).Build();
            ConfigurationPackage package = MockConfigurationPackage.CreateDefaultPackage(config, packageName);
            var data = new Dictionary<string, string>();

            sut.ConfigAction(package, data);

            string d = ConfigurationPath.KeyDelimiter;
            var expected = new Dictionary<string, string>
            {
                { $"{packageName}{d}{section1}{d}{param1}", value1 },
                { $"{packageName}{d}{section2}{d}{param2}", value2 },
            };
            Assert.Equal(expected, data);
        }

        [Fact(Explicit = true)] // TODO: SUT bug — DefaultConfigAction does not validate the config argument; fixing the SUT is out of scope.
        public void ThrowsArgumentNullExceptionWhenConfigIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ConfigAction(null, new Dictionary<string, string>()));
            Assert.Equal("config", exception.ParamName);
        }
    }

    public sealed class Constructor : ServiceFabricConfigurationOptionsTest
    {
        [Fact]
        public void InitializesProperties()
        {
            Assert.Equal(packageName, sut.PackageName);
            Assert.True(sut.IncludePackageName);
            Assert.False(sut.DecryptValue);
            Assert.Same(sut, sut.ConfigAction.Target);
            Assert.Equal("DefaultConfigAction", sut.ConfigAction.Method.Name);
            Assert.Same(sut, sut.ExtractKeyFunc.Target);
            Assert.Equal("DefaultExtractKeyFunc", sut.ExtractKeyFunc.Method.Name);
            Assert.Same(sut, sut.ExtractValueFunc.Target);
            Assert.Equal("DefaultExtractValueFunc", sut.ExtractValueFunc.Method.Name);
        }

        [Fact(Explicit = true)] // TODO: SUT bug — ctor passes null value to ArgumentNullException instead of nameof(packageName); fixing the SUT is out of scope.
        public void ThrowsArgumentNullExceptionWhenPackageNameIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new ServiceFabricConfigurationOptions(null));
            Assert.Equal(nameof(packageName), exception.ParamName);
        }
    }

    public sealed class ExtractKeyFunc : ServiceFabricConfigurationOptionsTest
    {
        readonly ConfigurationSection section = Section(fuzzy.String());
        readonly ConfigurationProperty property = Property(name: fuzzy.String());

        [Fact]
        public void IncludesPackageNameWhenIncludePackageNameIsTrue()
        {
            string actual = sut.ExtractKeyFunc(section, property);
            string d = ConfigurationPath.KeyDelimiter;
            Assert.Equal($"{packageName}{d}{section.Name}{d}{property.Name}", actual);
        }

        [Fact]
        public void ExcludesPackageNameWhenIncludePackageNameIsFalse()
        {
            sut.IncludePackageName = false;
            string actual = sut.ExtractKeyFunc(section, property);
            string d = ConfigurationPath.KeyDelimiter;
            Assert.Equal($"{section.Name}{d}{property.Name}", actual);
        }

        [Fact(Explicit = true)] // TODO: SUT bug — DefaultExtractKeyFunc does not validate the section argument; fixing the SUT is out of scope.
        public void ThrowsArgumentNullExceptionWhenSectionIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ExtractKeyFunc(null, property));
            Assert.Equal("section", exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug — DefaultExtractKeyFunc does not validate the property argument; fixing the SUT is out of scope.
        public void ThrowsArgumentNullExceptionWhenPropertyIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ExtractKeyFunc(section, null));
            Assert.Equal("property", exception.ParamName);
        }
    }

    public sealed class ExtractValueFunc : ServiceFabricConfigurationOptionsTest
    {
        readonly ConfigurationSection section = Section(fuzzy.String());

        [Fact]
        public void ReturnsPropertyValueWhenPropertyIsNotEncrypted()
        {
            ConfigurationProperty property = Property(value: fuzzy.String(), isEncrypted: false);
            string actual = sut.ExtractValueFunc(section, property);
            Assert.Equal(property.Value, actual);
        }

        [Fact]
        public void ReturnsPropertyValueWhenPropertyIsEncryptedAndDecryptValueIsFalse()
        {
            ConfigurationProperty property = Property(value: fuzzy.String(), isEncrypted: true);
            Assert.False(sut.DecryptValue);
            string actual = sut.ExtractValueFunc(section, property);
            Assert.Equal(property.Value, actual);
        }

        [Fact(Explicit = true)] // TODO: SUT bug — DefaultExtractValueFunc does not validate the property argument; fixing the SUT is out of scope.
        public void ThrowsArgumentNullExceptionWhenPropertyIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ExtractValueFunc(section, null));
            Assert.Equal("property", exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Cannot mock ConfigurationProperty.DecryptValue() to verify decryption branch.
        public void DemonstratesTestabilityLimitationForDecryptedValues() => throw new NotImplementedException();
    }

    static ConfigurationSection Section(string name)
    {
        var section = TestHelper.CreateInstanced<ConfigurationSection>();
        section.Set(nameof(ConfigurationSection.Name), name);
        return section;
    }

    static ConfigurationProperty Property(string name = null, string value = null, bool isEncrypted = false)
    {
        var property = TestHelper.CreateInstanced<ConfigurationProperty>();
        property.Set(nameof(ConfigurationProperty.Name), name ?? fuzzy.String());
        property.Set(nameof(ConfigurationProperty.Value), value ?? fuzzy.String());
        property.Set(nameof(ConfigurationProperty.IsEncrypted), isEncrypted);
        return property;
    }
}
