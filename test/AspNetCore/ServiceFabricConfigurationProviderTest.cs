// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric;
using Fuzzy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Microsoft.ServiceFabric.AspNetCore.Tests;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.AspNetCore.Configuration;

public abstract class ServiceFabricConfigurationProviderTest
{
    readonly ServiceFabricConfigurationProvider sut;

    // Constructor parameters
    readonly ICodePackageActivationContext activationContext = Mock.Of<ICodePackageActivationContext>();
    readonly ServiceFabricConfigurationOptions options;

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);
    readonly string packageName = fuzzy.String().LettersOrDigits();

    ServiceFabricConfigurationProviderTest()
    {
        options = new(packageName);
        sut = new ServiceFabricConfigurationProvider(activationContext, options);
    }

    public sealed class Constructor : ServiceFabricConfigurationProviderTest
    {
        readonly Mock<Action<ConfigurationPackage, IDictionary<string, string>>> configAction = new();
        readonly ConfigurationPackage matchingPackage;

        public Constructor()
        {
            matchingPackage = MockConfigurationPackage.CreateDefaultPackage(new ConfigurationBuilder().Build(), packageName);
            options.ConfigAction = configAction.Object;
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Constructor doesn't validate activationContext.
        public void ThrowsArgumentNullExceptionWhenActivationContextIsNull()
        {
            // The constructor stores activationContext without a null check and then dereferences it
            // when subscribing to ConfigurationPackageModifiedEvent, causing NullReferenceException
            // instead of an ArgumentNullException naming the offending parameter.
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ServiceFabricConfigurationProvider(null, options));
            Assert.Equal(nameof(activationContext), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenOptionsIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ServiceFabricConfigurationProvider(activationContext, null));
            Assert.Equal(nameof(options), exception.ParamName);
        }

        [Fact]
        public void InvokesConfigActionWhenAddedPackageNameMatches()
        {
            RaiseAdded(matchingPackage);
            configAction.Verify(_ => _(matchingPackage, It.IsAny<IDictionary<string, string>>()), Times.Once);
            configAction.Verify(_ => _(It.IsAny<ConfigurationPackage>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
        }

        [Fact]
        public void SignalsReloadTokenWhenAddedPackageNameMatches()
        {
            IChangeToken token = sut.GetReloadToken();
            RaiseAdded(matchingPackage);
            Assert.True(token.HasChanged);
        }

        [Fact]
        public void ClearsPreviouslyLoadedDataWhenAddedPackageNameMatches()
        {
            string staleKey = fuzzy.String().LettersOrDigits();
            sut.Set(staleKey, fuzzy.String().LettersOrDigits());

            RaiseAdded(matchingPackage);

            Assert.False(sut.TryGet(staleKey, out _));
        }

        [Fact]
        public void IgnoresAddedPackageWhenNameDoesNotMatch()
        {
            ConfigurationPackage otherPackage =
                MockConfigurationPackage.CreateDefaultPackage(new ConfigurationBuilder().Build(), packageName + fuzzy.String().LettersOrDigits());
            string existingKey = fuzzy.String().LettersOrDigits();
            string existingValue = fuzzy.String().LettersOrDigits();
            sut.Set(existingKey, existingValue);
            IChangeToken token = sut.GetReloadToken();

            RaiseAdded(otherPackage);

            configAction.Verify(
                _ => _(It.IsAny<ConfigurationPackage>(), It.IsAny<IDictionary<string, string>>()),
                Times.Never);
            Assert.False(token.HasChanged);
            Assert.True(sut.TryGet(existingKey, out string actualValue));
            Assert.Equal(existingValue, actualValue);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenAddedPackageDescriptionIsNull()
        {
            var package = TestHelper.CreateInstanced<ConfigurationPackage>();

            var exception = Assert.Throws<ArgumentNullException>(() => RaiseAdded(package));
            Assert.Equal("package.Description", exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. HandleNewPackage dereferences package without a null check.
        public void ThrowsArgumentNullExceptionWhenAddedPackageIsNull()
        {
            // HandleNewPackage accesses package.Description immediately, throwing NullReferenceException
            // instead of an ArgumentNullException naming the offending parameter.
            var exception = Assert.Throws<ArgumentNullException>(() => RaiseAdded(null));
            Assert.Equal("package", exception.ParamName);
        }

        [Fact]
        public void InvokesConfigActionWhenModifiedPackageNameMatches()
        {
            RaiseModified(null, matchingPackage);
            configAction.Verify(_ => _(matchingPackage, It.IsAny<IDictionary<string, string>>()), Times.Once);
            configAction.Verify(_ => _(It.IsAny<ConfigurationPackage>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
        }

        [Fact]
        public void SignalsReloadTokenWhenModifiedPackageNameMatches()
        {
            IChangeToken token = sut.GetReloadToken();
            RaiseModified(null, matchingPackage);
            Assert.True(token.HasChanged);
        }

        [Fact]
        public void ClearsPreviouslyLoadedDataWhenModifiedPackageNameMatches()
        {
            string staleKey = fuzzy.String().LettersOrDigits();
            sut.Set(staleKey, fuzzy.String().LettersOrDigits());

            RaiseModified(null, matchingPackage);

            Assert.False(sut.TryGet(staleKey, out _));
        }

        [Fact]
        public void IgnoresModifiedPackageWhenNameDoesNotMatch()
        {
            ConfigurationPackage otherPackage =
                MockConfigurationPackage.CreateDefaultPackage(new ConfigurationBuilder().Build(), packageName + fuzzy.String().LettersOrDigits());
            string existingKey = fuzzy.String().LettersOrDigits();
            string existingValue = fuzzy.String().LettersOrDigits();
            sut.Set(existingKey, existingValue);
            IChangeToken token = sut.GetReloadToken();

            RaiseModified(null, otherPackage);

            configAction.Verify(
                _ => _(It.IsAny<ConfigurationPackage>(), It.IsAny<IDictionary<string, string>>()),
                Times.Never);
            Assert.False(token.HasChanged);
            Assert.True(sut.TryGet(existingKey, out string actualValue));
            Assert.Equal(existingValue, actualValue);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenModifiedPackageDescriptionIsNull()
        {
            var package = TestHelper.CreateInstanced<ConfigurationPackage>();

            var exception = Assert.Throws<ArgumentNullException>(() => RaiseModified(null, package));
            Assert.Equal("package.Description", exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. HandleNewPackage dereferences package without a null check.
        public void ThrowsArgumentNullExceptionWhenModifiedPackageIsNull()
        {
            // HandleNewPackage accesses package.Description immediately, throwing NullReferenceException
            // instead of an ArgumentNullException naming the offending parameter.
            var exception = Assert.Throws<ArgumentNullException>(() => RaiseModified(null, null));
            Assert.Equal("package", exception.ParamName);
        }
    }

    public sealed class Load : ServiceFabricConfigurationProviderTest
    {
        readonly Mock<Action<ConfigurationPackage, IDictionary<string, string>>> configAction = new();
        readonly ConfigurationPackage package;

        public Load()
        {
            package = MockConfigurationPackage.CreateDefaultPackage(new ConfigurationBuilder().Build(), packageName);
            options.ConfigAction = configAction.Object;
            _ = Mock.Get(activationContext).Setup(_ => _.GetConfigurationPackageObject(packageName)).Returns(package);
        }

        [Fact]
        public void InvokesConfigActionWithPackageFromActivationContext()
        {
            sut.Load();
            configAction.Verify(_ => _(package, It.IsAny<IDictionary<string, string>>()), Times.Once);
            configAction.Verify(_ => _(It.IsAny<ConfigurationPackage>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
        }

        [Fact]
        public void PopulatesDataWithEntriesAddedByConfigAction()
        {
            string key = fuzzy.String().LettersOrDigits();
            string value = fuzzy.String().LettersOrDigits();
            _ = configAction.Setup(_ => _(package, It.IsAny<IDictionary<string, string>>()))
                .Callback((ConfigurationPackage _, IDictionary<string, string> data) => data[key] = value);

            sut.Load();

            Assert.True(sut.TryGet(key, out string actual));
            Assert.Same(value, actual);
        }

        [Fact]
        public void PreservesPreviouslyLoadedData()
        {
            string existingKey = fuzzy.String().LettersOrDigits();
            string existingValue = fuzzy.String().LettersOrDigits();
            sut.Set(existingKey, existingValue);

            sut.Load();

            Assert.True(sut.TryGet(existingKey, out string actual));
            Assert.Same(existingValue, actual);
        }
    }

    void RaiseModified(ConfigurationPackage oldPackage, ConfigurationPackage newPackage) =>
        Mock.Get(activationContext).Raise(_ => _.ConfigurationPackageModifiedEvent += null,
            new PackageModifiedEventArgs<ConfigurationPackage> { OldPackage = oldPackage, NewPackage = newPackage });

    void RaiseAdded(ConfigurationPackage package) =>
        Mock.Get(activationContext).Raise(_ => _.ConfigurationPackageAddedEvent += null,
            new PackageAddedEventArgs<ConfigurationPackage> { Package = package });
}
