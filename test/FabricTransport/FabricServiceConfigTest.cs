// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Fabric.Management.ServiceModel;
using System.IO;
using Fuzzy;
using Inspector;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.FabricTransport;

[WindowsOnly("Can't load libFabricCommon.so on Linux.")]
public abstract class FabricServiceConfigTest: FabricServiceConfigAccessor
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    public sealed class GetConfig: FabricServiceConfigTest
    {
        readonly string settingsFile = CreateSettingsFile();

        public override void Dispose()
        {
            File.Delete(settingsFile);
            File.Delete(EntrySettingsFile.Path);
            base.Dispose();
        }

        [Fact]
        public void ReturnsInstanceCreatedByPriorInitialize()
        {
            // Stage an entry-assembly settings file so the fallback path in GetConfig would observably
            // overwrite `instance` if the `if (instance == null)` fast-path guard regressed.
            EntrySettingsFile.AssertAbsent();
            File.WriteAllText(EntrySettingsFile.Path, EmptySettings);
            SettingsType expected = new();
            IFabricServiceConfigParser configParser = Mock.Of<IFabricServiceConfigParser>(_ => _.Parse(settingsFile) == expected);
            _ = FabricServiceConfig.Initialize(settingsFile, configParser);
            var initial = FabricServiceConfig.GetConfig();

            var actual = FabricServiceConfig.GetConfig();

            Assert.Same(initial, actual);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Depends on FabricRuntime.GetActivationContext().
        public void ReturnsInstanceInitializedFromConfigPackageAndSkipsEntryAssemblyFallbackWhenConfigPackageIsAvailable() =>
            // When InitializeFromConfigPkgWithCallerHoldingLock(DefaultPackageName) succeeds, GetConfig skips the
            // entry-assembly fallback and returns the instance populated from the config package. Reaching this
            // branch requires FabricRuntime.GetActivationContext() to expose a "Config" package, which is only
            // available inside a Service Fabric host process and cannot be substituted in a unit test.
            throw new NotImplementedException();

        [Fact]
        public void LazilyInitializesFromEntryAssemblySettingsFileWhenNotYetInitialized()
        {
            // Outside an SF host FabricRuntime.GetActivationContext() throws, so GetConfig falls through to the
            // entry-assembly path: <entry-assembly-dir>/<entry-assembly-name>.Settings.xml. Under this test
            // project the test runner is the entry assembly, so we can stage that file next to it. The staged
            // file uses a unique section name so the assertion verifies GetConfig read this specific file.
            EntrySettingsFile.AssertAbsent();
            string sectionName = "EntrySettings_" + fuzzy.String().LettersOrDigits();
            File.WriteAllText(EntrySettingsFile.Path,
                $"""
                <Settings xmlns="http://schemas.microsoft.com/2011/01/fabric">
                  <Section Name="{sectionName}" />
                </Settings>
                """);
            var actual = FabricServiceConfig.GetConfig();

            SettingsTypeSection section = Assert.Single(actual.Settings.Section);
            Assert.Equal(sectionName, section.Name);
        }

        [Fact]
        public void ReturnsNullWhenNoInitializationPathSucceeds()
        {
            // Outside an SF host FabricRuntime.GetActivationContext() throws, and without staging the
            // entry-assembly settings file the exe-settings path also fails. GetConfig then returns the
            // instance left behind by InitializeWithCallerHoldingLock, which is the null reset by the base.
            EntrySettingsFile.AssertAbsent();
            Assert.Null(FabricServiceConfig.GetConfig());
        }
    }

    public sealed class Initialize: FabricServiceConfigTest
    {
        readonly string fullFilePath = CreateSettingsFile();
        readonly Mock<IFabricServiceConfigParser> configParser = new();

        public override void Dispose()
        {
            File.Delete(fullFilePath);
            base.Dispose();
        }

        [Fact]
        public void ReturnsTrueAndStoresParsedSettingsWhenFileExistsAndConfigParserIsProvided()
        {
            SettingsType expected = new();
            _ = configParser.Setup(_ => _.Parse(fullFilePath)).Returns(expected);

            bool result = FabricServiceConfig.Initialize(fullFilePath, configParser.Object);

            Assert.True(result);
            var config = FabricServiceConfig.GetConfig();
            Assert.Same(expected, config.Settings);
            configParser.Verify(_ => _.Parse(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ParsesFileWithDefaultParserWhenConfigParserIsNull()
        {
            // Integration-style by necessity: the SUT instantiates the default SettingsConfigParser inline
            // when configParser is null, so there is no seam to substitute it. This test generates its own
            // settings file with a fuzzy section name and exercises the real parser against it.
            string sectionName = "Section_" + fuzzy.String().LettersOrDigits();
            File.WriteAllText(fullFilePath,
                $"""
                <Settings xmlns="http://schemas.microsoft.com/2011/01/fabric">
                  <Section Name="{sectionName}" />
                </Settings>
                """);
            bool result = FabricServiceConfig.Initialize(fullFilePath, null);

            Assert.True(result);
            SettingsType settings = FabricServiceConfig.GetConfig().Settings;
            SettingsTypeSection section = Assert.Single(settings.Section);
            Assert.Equal(sectionName, section.Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReturnsFalseWhenFullFilePathIsNullOrEmpty(string fullFilePath) =>
            Assert.False(FabricServiceConfig.Initialize(fullFilePath, configParser.Object));

        [Fact]
        public void ReturnsFalseAndLeavesInstanceUnchangedWhenFileDoesNotExist()
        {
            SettingsType expected = new();
            _ = configParser.Setup(_ => _.Parse(fullFilePath)).Returns(expected);
            Assert.True(FabricServiceConfig.Initialize(fullFilePath, configParser.Object));
            var initial = FabricServiceConfig.GetConfig();
            string missing = Path.Combine(Path.GetTempPath(), fuzzy.String().LettersOrDigits() + ".xml");
            Assert.False(File.Exists(missing), $"Pre-existing {missing} would invalidate this test.");

            bool result = FabricServiceConfig.Initialize(missing, configParser.Object);

            Assert.False(result);
            Assert.Same(initial, FabricServiceConfig.GetConfig());
            configParser.Verify(_ => _.Parse(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ReplacesPriorInstanceOnSubsequentSuccessfulInitialize()
        {
            SettingsType first = new();
            SettingsType second = new();
            _ = configParser.SetupSequence(_ => _.Parse(fullFilePath)).Returns(first).Returns(second);
            _ = FabricServiceConfig.Initialize(fullFilePath, configParser.Object);
            var initial = FabricServiceConfig.GetConfig();

            bool result = FabricServiceConfig.Initialize(fullFilePath, configParser.Object);

            Assert.True(result);
            var current = FabricServiceConfig.GetConfig();
            Assert.NotSame(initial, current);
            Assert.Same(second, current.Settings);
            configParser.Verify(_ => _.Parse(It.IsAny<string>()), Times.Exactly(2));
        }
    }

    public sealed class InitializeFromConfigPackage: FabricServiceConfigTest
    {
        // Method parameters
        readonly string configPackageName = fuzzy.String();

        readonly string settingsFile = CreateSettingsFile();

        public override void Dispose()
        {
            File.Delete(settingsFile);
            base.Dispose();
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Depends on FabricRuntime.GetActivationContext().
        public void ReturnsTrueAndStoresConfigurationSettingsWhenActivationContextProvidesConfigPackage() =>
            // The success path of InitializeFromConfigPackage calls FabricRuntime.GetActivationContext(),
            // which is only available inside a Service Fabric host process and cannot be substituted in a unit test.
            throw new NotImplementedException();

        [Fact]
        public void ReturnsFalseAndLeavesInstanceUnchangedWhenActivationContextIsUnavailable()
        {
            SettingsType expected = new();
            IFabricServiceConfigParser configParser = Mock.Of<IFabricServiceConfigParser>(_ => _.Parse(settingsFile) == expected);
            Assert.True(FabricServiceConfig.Initialize(settingsFile, configParser));
            var initial = FabricServiceConfig.GetConfig();

            bool result = FabricServiceConfig.InitializeFromConfigPackage(configPackageName);

            Assert.False(result);
            Assert.Same(initial, FabricServiceConfig.GetConfig());
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Depends on FabricRuntime.GetActivationContext().
        public void ReturnsFalseWhenConfigPackageIsUnavailable() =>
            // TryGetConfigPackageObject returns false when the activation context exists but does not expose
            // a configuration package with the requested name. Exercising this branch requires substituting
            // FabricRuntime.GetActivationContext(), which is only available inside a Service Fabric host process.
            throw new NotImplementedException();
    }

    const string EmptySettings = """<Settings xmlns="http://schemas.microsoft.com/2011/01/fabric"/>""";

    static string CreateSettingsFile()
    {
        string path = Path.Combine(Path.GetTempPath(), fuzzy.String().LettersOrDigits() + ".xml");
        File.WriteAllText(path, EmptySettings);
        return path;
    }
}
