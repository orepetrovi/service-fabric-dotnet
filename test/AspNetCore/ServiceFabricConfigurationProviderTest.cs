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
using Microsoft.Extensions.Primitives;
using Microsoft.ServiceFabric.AspNetCore.Tests;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.AspNetCore.Configuration;

public abstract class ServiceFabricConfigurationProviderTest
{
    readonly ServiceFabricConfigurationProvider sut;

    // Constructor parameters
    readonly Mock<ICodePackageActivationContext> activationContext = new();
    readonly ServiceFabricConfigurationOptions options;

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);
    readonly string packageName = fuzzy.String();
    readonly Mock<Action<ConfigurationPackage, IDictionary<string, string>>> configAction = new();

    ServiceFabricConfigurationProviderTest()
    {
        options = new(packageName) { ConfigAction = configAction.Object };
        sut = new ServiceFabricConfigurationProvider(activationContext.Object, options);
    }

    public sealed class Constructor : ServiceFabricConfigurationProviderTest
    {
        readonly ConfigurationPackage matching;

        public Constructor()
        {
            matching = MockConfigurationPackage.CreateDefaultPackage(new ConfigurationBuilder().Build(), packageName);
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
                () => new ServiceFabricConfigurationProvider(activationContext.Object, null));
            Assert.Equal(nameof(options), exception.ParamName);
        }

        [Fact]
        public void InvokesConfigActionWhenAddedPackageNameMatches()
        {
            RaiseAdded(matching);
            configAction.Verify(_ => _(matching, It.IsAny<IDictionary<string, string>>()), Times.Once);
            configAction.Verify(_ => _(It.IsAny<ConfigurationPackage>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
        }

        [Fact]
        public void SignalsReloadTokenWhenAddedPackageNameMatches()
        {
            IChangeToken token = sut.GetReloadToken();
            RaiseAdded(matching);
            Assert.True(token.HasChanged);
        }

        [Fact]
        public void ClearsPreviouslyLoadedDataWhenAddedPackageNameMatches()
        {
            string staleKey = fuzzy.String();
            sut.Set(staleKey, fuzzy.String());

            RaiseAdded(matching);

            Assert.False(sut.TryGet(staleKey, out _));
        }

        [Fact]
        public void PopulatesDataWithEntriesAddedByConfigActionWhenAddedPackageNameMatches()
        {
            string key = fuzzy.String();
            string value = fuzzy.String();
            _ = configAction.Setup(_ => _(matching, It.IsAny<IDictionary<string, string>>()))
                .Callback((ConfigurationPackage _, IDictionary<string, string> data) => data[key] = value);

            RaiseAdded(matching);

            Assert.True(sut.TryGet(key, out string actual));
            Assert.Same(value, actual);
        }

        [Fact]
        public void IgnoresAddedPackageWhenNameDoesNotMatch()
        {
            ConfigurationPackage other =
                MockConfigurationPackage.CreateDefaultPackage(new ConfigurationBuilder().Build(), packageName + fuzzy.String());
            string existingKey = fuzzy.String();
            string existingValue = fuzzy.String();
            sut.Set(existingKey, existingValue);
            IChangeToken token = sut.GetReloadToken();

            RaiseAdded(other);

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
            var package = Type<ConfigurationPackage>.Uninitialized();

            var exception = Assert.Throws<ArgumentNullException>(() => RaiseAdded(package));
            Assert.Equal($"{nameof(package)}.{nameof(ConfigurationPackage.Description)}", exception.ParamName);
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
            RaiseModified(matching);
            configAction.Verify(_ => _(matching, It.IsAny<IDictionary<string, string>>()), Times.Once);
            configAction.Verify(_ => _(It.IsAny<ConfigurationPackage>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
        }

        [Fact]
        public void SignalsReloadTokenWhenModifiedPackageNameMatches()
        {
            IChangeToken token = sut.GetReloadToken();
            RaiseModified(matching);
            Assert.True(token.HasChanged);
        }

        [Fact]
        public void ClearsPreviouslyLoadedDataWhenModifiedPackageNameMatches()
        {
            string staleKey = fuzzy.String();
            sut.Set(staleKey, fuzzy.String());

            RaiseModified(matching);

            Assert.False(sut.TryGet(staleKey, out _));
        }

        [Fact]
        public void PopulatesDataWithEntriesAddedByConfigActionWhenModifiedPackageNameMatches()
        {
            string key = fuzzy.String();
            string value = fuzzy.String();
            _ = configAction.Setup(_ => _(matching, It.IsAny<IDictionary<string, string>>()))
                .Callback((ConfigurationPackage _, IDictionary<string, string> data) => data[key] = value);

            RaiseModified(matching);

            Assert.True(sut.TryGet(key, out string actual));
            Assert.Same(value, actual);
        }

        [Fact]
        public void IgnoresModifiedPackageWhenNameDoesNotMatch()
        {
            ConfigurationPackage other =
                MockConfigurationPackage.CreateDefaultPackage(new ConfigurationBuilder().Build(), packageName + fuzzy.String());
            string existingKey = fuzzy.String();
            string existingValue = fuzzy.String();
            sut.Set(existingKey, existingValue);
            IChangeToken token = sut.GetReloadToken();

            RaiseModified(other);

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
            var package = Type<ConfigurationPackage>.Uninitialized();

            var exception = Assert.Throws<ArgumentNullException>(() => RaiseModified(package));
            Assert.Equal($"{nameof(package)}.{nameof(ConfigurationPackage.Description)}", exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. HandleNewPackage dereferences package without a null check.
        public void ThrowsArgumentNullExceptionWhenModifiedPackageIsNull()
        {
            // HandleNewPackage accesses package.Description immediately, throwing NullReferenceException
            // instead of an ArgumentNullException naming the offending parameter.
            var exception = Assert.Throws<ArgumentNullException>(() => RaiseModified(null));
            Assert.Equal("package", exception.ParamName);
        }
    }

    public sealed class Load : ServiceFabricConfigurationProviderTest
    {
        readonly ConfigurationPackage package;

        public Load()
        {
            package = MockConfigurationPackage.CreateDefaultPackage(new ConfigurationBuilder().Build(), packageName);
            _ = activationContext.Setup(_ => _.GetConfigurationPackageObject(packageName)).Returns(package);
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
            string key = fuzzy.String();
            string value = fuzzy.String();
            _ = configAction.Setup(_ => _(package, It.IsAny<IDictionary<string, string>>()))
                .Callback((ConfigurationPackage _, IDictionary<string, string> data) => data[key] = value);

            sut.Load();

            Assert.True(sut.TryGet(key, out string actual));
            Assert.Same(value, actual);
        }

        [Fact]
        public void PreservesPreviouslyLoadedData()
        {
            string existingKey = fuzzy.String();
            string existingValue = fuzzy.String();
            sut.Set(existingKey, existingValue);

            sut.Load();

            Assert.True(sut.TryGet(existingKey, out string actual));
            Assert.Same(existingValue, actual);
        }
    }

    void RaiseModified(ConfigurationPackage newPackage) =>
        activationContext.Raise(_ => _.ConfigurationPackageModifiedEvent += null,
            new PackageModifiedEventArgs<ConfigurationPackage> { NewPackage = newPackage });

    void RaiseAdded(ConfigurationPackage package) =>
        activationContext.Raise(_ => _.ConfigurationPackageAddedEvent += null,
            new PackageAddedEventArgs<ConfigurationPackage> { Package = package });
}
