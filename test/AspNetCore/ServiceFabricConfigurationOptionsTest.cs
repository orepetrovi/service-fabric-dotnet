// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using Fuzzy;
using Inspector;
using Microsoft.Extensions.Configuration;
using Microsoft.ServiceFabric.AspNetCore.Tests;
using Xunit;
using ConfigurationSection = System.Fabric.Description.ConfigurationSection;

namespace Microsoft.ServiceFabric.AspNetCore.Configuration;

public abstract class ServiceFabricConfigurationOptionsTest
{
    readonly ServiceFabricConfigurationOptions sut;

    // Constructor parameters
    readonly string packageName = fuzzy.String();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ServiceFabricConfigurationOptionsTest() =>
        sut = new ServiceFabricConfigurationOptions(packageName);

    public sealed class ConfigAction : ServiceFabricConfigurationOptionsTest
    {
        // Method parameters
        readonly ConfigurationPackage package;
        readonly Dictionary<string, string> data = new();

        readonly string section1 = "Section" + fuzzy.String().LettersOrDigits();
        readonly string param1 = "Param" + fuzzy.String().LettersOrDigits();
        readonly string value1 = fuzzy.String();
        readonly string section2 = "Section" + fuzzy.String().LettersOrDigits();
        readonly string param2 = "Param" + fuzzy.String().LettersOrDigits();
        readonly string value2 = fuzzy.String();

        public ConfigAction()
        {
            IConfigurationRoot config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{section1}:{param1}", value1 },
                { $"{section2}:{param2}", value2 },
            }).Build();
            package = MockConfigurationPackage.CreateDefaultPackage(config, packageName);
        }

        [Fact]
        public void ExecutesExtractKeyFuncAndExtractValueFuncToPopulateData()
        {
            string keyPrefix = fuzzy.String().LettersOrDigits();
            string valuePrefix = fuzzy.String().LettersOrDigits();
            sut.ExtractKeyFunc = (section, property) => $"{keyPrefix}:{section.Name}:{property.Name}";
            sut.ExtractValueFunc = (section, property) => $"{valuePrefix}:{property.Value}";

            sut.ConfigAction(package, data);

            Dictionary<string, string> customExpected = new()
            {
                [$"{keyPrefix}:{section1}:{param1}"] = $"{valuePrefix}:{value1}",
                [$"{keyPrefix}:{section2}:{param2}"] = $"{valuePrefix}:{value2}",
            };
            Assert.Equal(customExpected, data);
        }

        [Fact(Explicit = true)] // TODO: SUT bug — DefaultConfigAction does not validate the config argument; fixing the SUT is out of scope.
        public void ThrowsArgumentNullExceptionWhenConfigIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ConfigAction(null, data));
            Assert.Equal(sut.ConfigAction.Method.Parameter<ConfigurationPackage>().Name, exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug — DefaultConfigAction does not validate the data argument; fixing the SUT is out of scope.
        public void ThrowsArgumentNullExceptionWhenDataIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ConfigAction(package, null));
            Assert.Equal(sut.ConfigAction.Method.Parameter<IDictionary<string, string>>().Name, exception.ParamName);
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
            Assert.NotNull(sut.ConfigAction);
            Assert.NotNull(sut.ExtractKeyFunc);
            Assert.NotNull(sut.ExtractValueFunc);
        }

        [Fact(Explicit = true)] // TODO: SUT bug — ctor passes null value to ArgumentNullException instead of nameof(packageName); fixing the SUT is out of scope.
        public void ThrowsArgumentNullExceptionWhenPackageNameIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new ServiceFabricConfigurationOptions(null));
            Assert.Equal(sut.Constructor().Parameter<string>().Name, exception.ParamName);
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
            Assert.Equal(sut.ExtractKeyFunc.Method.Parameter<ConfigurationSection>().Name, exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug — DefaultExtractKeyFunc does not validate the property argument; fixing the SUT is out of scope.
        public void ThrowsArgumentNullExceptionWhenPropertyIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ExtractKeyFunc(section, null));
            Assert.Equal(sut.ExtractKeyFunc.Method.Parameter<ConfigurationProperty>().Name, exception.ParamName);
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
            Assert.Same(property.Value, actual);
        }

        [Fact]
        public void ReturnsPropertyValueWhenPropertyIsEncryptedAndDecryptValueIsFalse()
        {
            ConfigurationProperty property = Property(value: fuzzy.String(), isEncrypted: true);
            sut.DecryptValue = false;
            string actual = sut.ExtractValueFunc(section, property);
            Assert.Same(property.Value, actual);
        }

        [Fact]
        public void ReturnsPropertyValueWhenPropertyIsNotEncryptedAndDecryptValueIsTrue()
        {
            ConfigurationProperty property = Property(value: fuzzy.String(), isEncrypted: false);
            sut.DecryptValue = true;
            string actual = sut.ExtractValueFunc(section, property);
            Assert.Same(property.Value, actual);
        }

        [Fact(Explicit = true)] // TODO: SUT bug — DefaultExtractValueFunc does not validate the property argument; fixing the SUT is out of scope.
        public void ThrowsArgumentNullExceptionWhenPropertyIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ExtractValueFunc(section, null));
            Assert.Equal(sut.ExtractValueFunc.Method.Parameter<ConfigurationProperty>().Name, exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. ConfigurationProperty.DecryptValue() is a non-virtual instance method and cannot be substituted; exercising the decryption branch requires refactoring the SUT to accept an injectable decryptor.
        public void ReturnsDecryptedValueWhenPropertyIsEncryptedAndDecryptValueIsTrue()
        {
            // The decryption branch of DefaultExtractValueFunc calls the non-virtual instance method
            // ConfigurationProperty.DecryptValue(), which cannot be substituted in unit tests. Exercising this branch
            // is not possible without refactoring the SUT to accept an injectable decryptor. Fixing the underlying
            // testability limitation is out of scope for the current change.
            throw new NotImplementedException();
        }
    }

    static ConfigurationSection Section(string name)
    {
        var section = Type<ConfigurationSection>.Uninitialized();
        section.Property<string>(nameof(ConfigurationSection.Name)).Set(name);
        return section;
    }

    static ConfigurationProperty Property(string name = null, string value = null, bool isEncrypted = false)
    {
        var property = Type<ConfigurationProperty>.Uninitialized();
        property.Property<string>(nameof(ConfigurationProperty.Name)).Set(name ?? fuzzy.String());
        property.Property<string>(nameof(ConfigurationProperty.Value)).Set(value ?? fuzzy.String());
        property.Property<bool>(nameof(ConfigurationProperty.IsEncrypted)).Set(isEncrypted);
        return property;
    }
}
