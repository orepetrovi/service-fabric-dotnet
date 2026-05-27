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
using Moq;
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
        readonly ConfigurationPackage config;
        readonly IDictionary<string, string> data = new Dictionary<string, string>();

        readonly string section1 = fuzzy.String().LettersOrDigits();
        readonly string param1a = fuzzy.String().LettersOrDigits();
        readonly string param1b = fuzzy.String().LettersOrDigits();
        readonly string section2 = fuzzy.String().LettersOrDigits();
        readonly string param2 = fuzzy.String().LettersOrDigits();

        // Placeholder satisfies MockConfigurationPackage.CreateDefaultPackage; ExtractValueFunc is mocked in the test
        // so the actual parameter values are never read.
        const string unused = "";

        public ConfigAction()
        {
            IConfigurationRoot root = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{section1}:{param1a}", unused },
                { $"{section1}:{param1b}", unused },
                { $"{section2}:{param2}", unused },
            }).Build();
            config = MockConfigurationPackage.CreateDefaultPackage(root, packageName);
        }

        [Fact]
        public void ExecutesExtractKeyFuncAndExtractValueFuncToPopulateData()
        {
            // Arrange
            ConfigurationSection sec1 = config.Settings.Sections[section1];
            ConfigurationSection sec2 = config.Settings.Sections[section2];
            ConfigurationProperty p1a = sec1.Parameters[param1a];
            ConfigurationProperty p1b = sec1.Parameters[param1b];
            ConfigurationProperty p2 = sec2.Parameters[param2];

            string key1a = fuzzy.String();
            string key1b = fuzzy.String();
            string key2 = fuzzy.String();
            string val1a = fuzzy.String();
            string val1b = fuzzy.String();
            string val2 = fuzzy.String();

            Mock<Func<ConfigurationSection, ConfigurationProperty, string>> extractKey = new();
            _ = extractKey.Setup(_ => _(sec1, p1a)).Returns(key1a);
            _ = extractKey.Setup(_ => _(sec1, p1b)).Returns(key1b);
            _ = extractKey.Setup(_ => _(sec2, p2)).Returns(key2);
            sut.ExtractKeyFunc = extractKey.Object;

            Mock<Func<ConfigurationSection, ConfigurationProperty, string>> extractValue = new();
            _ = extractValue.Setup(_ => _(sec1, p1a)).Returns(val1a);
            _ = extractValue.Setup(_ => _(sec1, p1b)).Returns(val1b);
            _ = extractValue.Setup(_ => _(sec2, p2)).Returns(val2);
            sut.ExtractValueFunc = extractValue.Object;

            // Act
            sut.ConfigAction(config, data);

            // Assert
            Dictionary<string, string> expected = new()
            {
                [key1a] = val1a,
                [key1b] = val1b,
                [key2] = val2,
            };
            Assert.Equal(expected, data);

            extractKey.Verify(_ => _(It.IsAny<ConfigurationSection>(), It.IsAny<ConfigurationProperty>()), Times.Exactly(3));
            extractValue.Verify(_ => _(It.IsAny<ConfigurationSection>(), It.IsAny<ConfigurationProperty>()), Times.Exactly(3));
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing config argument validation.
        public void ThrowsArgumentNullExceptionWhenConfigIsNull()
        {
            // DefaultConfigAction dereferences its `config` parameter without a null check, so passing null produces a
            // NullReferenceException instead of the ArgumentNullException expected for a public-facing delegate. This
            // test asserts the correct behavior and will fail until the SUT validates the argument. Fixing the SUT is
            // out of scope for the current change.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ConfigAction(null, data));
            Assert.Equal(sut.ConfigAction.Method.Parameter<ConfigurationPackage>().Name, exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing data argument validation.
        public void ThrowsArgumentNullExceptionWhenDataIsNull()
        {
            // DefaultConfigAction dereferences its `data` parameter without a null check, so passing null produces a
            // NullReferenceException instead of the ArgumentNullException expected for a public-facing delegate. This
            // test asserts the correct behavior and will fail until the SUT validates the argument. Fixing the SUT is
            // out of scope for the current change.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ConfigAction(config, null));
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

        [Fact(Explicit = true)] // TODO: SUT bug. Missing packageName argument validation.
        public void ThrowsArgumentNullExceptionWhenPackageNameIsNull()
        {
            // The constructor throws `new ArgumentNullException(packageName)`, passing the (null) value as the
            // paramName argument instead of `nameof(packageName)`. As a result, the resulting exception's ParamName is
            // null and gives callers no indication which argument was invalid. This test asserts the correct paramName
            // and will fail until the SUT is fixed. Fixing the SUT is out of scope for the current change.
            var exception = Assert.Throws<ArgumentNullException>(() => new ServiceFabricConfigurationOptions(null));
            Assert.Equal(sut.Constructor().Parameter<string>().Name, exception.ParamName);
        }
    }

    public sealed class ExtractKeyFunc : ServiceFabricConfigurationOptionsTest
    {
        readonly ConfigurationSection section = Section();
        readonly ConfigurationProperty property = Property();

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

        [Fact(Explicit = true)] // TODO: SUT bug. Missing section argument validation.
        public void ThrowsArgumentNullExceptionWhenSectionIsNull()
        {
            // DefaultExtractKeyFunc dereferences its `section` parameter without a null check, so passing null produces
            // a NullReferenceException instead of the ArgumentNullException expected for a public-facing delegate. This
            // test asserts the correct behavior and will fail until the SUT validates the argument. Fixing the SUT is
            // out of scope for the current change.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ExtractKeyFunc(null, property));
            Assert.Equal(sut.ExtractKeyFunc.Method.Parameter<ConfigurationSection>().Name, exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing property argument validation.
        public void ThrowsArgumentNullExceptionWhenPropertyIsNull()
        {
            // DefaultExtractKeyFunc dereferences its `property` parameter without a null check, so passing null
            // produces a NullReferenceException instead of the ArgumentNullException expected for a public-facing
            // delegate. This test asserts the correct behavior and will fail until the SUT validates the argument.
            // Fixing the SUT is out of scope for the current change.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ExtractKeyFunc(section, null));
            Assert.Equal(sut.ExtractKeyFunc.Method.Parameter<ConfigurationProperty>().Name, exception.ParamName);
        }
    }

    public sealed class ExtractValueFunc : ServiceFabricConfigurationOptionsTest
    {
        readonly ConfigurationSection section = Section();
        readonly ConfigurationProperty property = Property();

        [Fact]
        public void ReturnsPropertyValueWhenPropertyIsNotEncrypted()
        {
            string actual = sut.ExtractValueFunc(section, property);
            Assert.Same(property.Value, actual);
        }

        [Fact]
        public void ReturnsPropertyValueWhenPropertyIsEncryptedAndDecryptValueIsFalse()
        {
            ConfigurationProperty encryptedProperty = Property(value: fuzzy.String(), isEncrypted: true);
            sut.DecryptValue = false;
            string actual = sut.ExtractValueFunc(section, encryptedProperty);
            Assert.Same(encryptedProperty.Value, actual);
        }

        [Fact]
        public void ReturnsPropertyValueWhenPropertyIsNotEncryptedAndDecryptValueIsTrue()
        {
            sut.DecryptValue = true;
            string actual = sut.ExtractValueFunc(section, property);
            Assert.Same(property.Value, actual);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Decryption branch cannot be substituted.
        public void ReturnsDecryptedValueWhenPropertyIsEncryptedAndDecryptValueIsTrue()
        {
            // The decryption branch of DefaultExtractValueFunc calls the non-virtual instance method
            // ConfigurationProperty.DecryptValue(), which cannot be substituted in unit tests. Exercising this branch
            // is not possible without refactoring the SUT to accept an injectable decryptor. Fixing the underlying
            // testability limitation is out of scope for the current change.
            throw new NotImplementedException();
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing property argument validation.
        public void ThrowsArgumentNullExceptionWhenPropertyIsNull()
        {
            // DefaultExtractValueFunc dereferences its `property` parameter without a null check, so passing null
            // produces a NullReferenceException instead of the ArgumentNullException expected for a public-facing
            // delegate. This test asserts the correct behavior and will fail until the SUT validates the argument.
            // Fixing the SUT is out of scope for the current change.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.ExtractValueFunc(section, null));
            Assert.Equal(sut.ExtractValueFunc.Method.Parameter<ConfigurationProperty>().Name, exception.ParamName);
        }
    }

    static ConfigurationSection Section(string name = null)
    {
        var section = Type<ConfigurationSection>.Uninitialized();
        section.Property<string>().Set(name ?? fuzzy.String());
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
