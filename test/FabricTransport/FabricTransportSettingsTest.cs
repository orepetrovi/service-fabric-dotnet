// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Fabric;
using System.Fabric.Interop;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Fuzzy;
using Xunit;
using static Microsoft.ServiceFabric.FabricTransport.NativeFabricTransport;

namespace Microsoft.ServiceFabric.FabricTransport;

public abstract class FabricTransportSettingsTest: FabricServiceConfigAccessor
{
    readonly FabricTransportSettings sut = new();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    public sealed class Constructor: FabricTransportSettingsTest
    {
        [Fact]
        public void InitializesDefaults()
        {
            Assert.Equal(TimeSpan.FromMinutes(5), sut.OperationTimeout);
            Assert.Equal(TimeSpan.Zero, sut.KeepAliveTimeout);
            Assert.Equal(TimeSpan.FromSeconds(5), sut.ConnectTimeout);
            Assert.Equal(4 * 1024 * 1024, sut.MaxMessageSize);
            Assert.Equal(10000, sut.MaxQueueSize);
            Assert.Equal(0, sut.MaxConcurrentCalls);
            Assert.Equal(CredentialType.None, sut.SecurityCredentials.CredentialType);
        }
    }

    public sealed class ConnectTimeout: FabricTransportSettingsTest
    {
        [Fact]
        public void IsSetToGivenValue()
        {
            TimeSpan expected = fuzzy.TimeSpan();
            sut.ConnectTimeout = expected;
            Assert.Equal(expected, sut.ConnectTimeout);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class GetDefault: FabricTransportSettingsTest
    {
        string sectionName;

        public GetDefault() => EntrySettingsFile.AssertAbsent();

        public override void Dispose()
        {
            File.Delete(EntrySettingsFile.Path);
            base.Dispose();
        }

        [Fact]
        public void ReturnsSettingsWithDefaultValuesWhenSectionDoesNotExist()
        {
            sectionName = fuzzy.String().LettersOrDigits();

            var settings = FabricTransportSettings.GetDefault(sectionName);

            Assert.Equal(FabricTransportSettings.DefaultOperationTimeout, settings.OperationTimeout);
        }

        [Fact]
        public void ReadsTransportSettingsFromConfigWhenSectionIsPresent()
        {
            // Outside an SF host FabricServiceConfig.GetConfig falls back to the entry-assembly settings file,
            // which the test runner is, so staging that file routes GetDefault through the success branch of
            // TryLoadFrom. A timeout value of 0 is the sentinel for "use the default", so the timeout is
            // generated > 0 to differ from the constructor default, proving GetDefault returned the loaded settings.
            sectionName = fuzzy.String().LettersOrDigits();
            int operationTimeoutInSeconds = fuzzy.Int32().Minimum(1);
            File.WriteAllText(EntrySettingsFile.Path,
                $"""
                <?xml version="1.0" encoding="utf-8"?>
                <Settings xmlns="http://schemas.microsoft.com/2011/01/fabric">
                  <Section Name="{sectionName}">
                    <Parameter Name="OperationTimeoutInSeconds" Value="{operationTimeoutInSeconds}" />
                  </Section>
                </Settings>
                """);

            var settings = FabricTransportSettings.GetDefault(sectionName);

            Assert.Equal(TimeSpan.FromSeconds(operationTimeoutInSeconds), settings.OperationTimeout);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class InitializeSettingsFromConfig: FabricTransportSettingsTest
    {
        // Method parameters
        string sectionName;

        readonly string dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;

        public override void Dispose()
        {
            Directory.Delete(dir, recursive: true);
            base.Dispose();
        }

        [Fact]
        public void ReturnsTrueAndLoadsSettingsWhenSectionExists()
        {
            sectionName = fuzzy.String().LettersOrDigits();
            int operationSeconds = fuzzy.Int32().Minimum(1);
            _ = FabricServiceConfig.Initialize(CreateSettingsFile(dir, sectionName,
                $"""<Parameter Name="OperationTimeoutInSeconds" Value="{operationSeconds}" />"""));

            bool initialized = sut.InitializeSettingsFromConfig(sectionName);

            Assert.True(initialized);
            Assert.Equal(TimeSpan.FromSeconds(operationSeconds), sut.OperationTimeout);
        }

        [Fact]
        public void ReturnsFalseWhenSectionDoesNotExist()
        {
            sectionName = "AbsentSection";
            _ = FabricServiceConfig.Initialize(CreateSettingsFile(dir, "PresentSection", ""));
            Assert.False(sut.InitializeSettingsFromConfig(sectionName));
        }

        [Fact]
        public void UsesDefaultSectionNameWhenSectionNameIsNull()
        {
            // sectionName ?? DefaultSectionName routes a null sectionName to the DefaultSectionName section.
            int operationSeconds = fuzzy.Int32().Minimum(1);
            _ = FabricServiceConfig.Initialize(CreateSettingsFile(dir, FabricTransportSettings.DefaultSectionName,
                $"""<Parameter Name="OperationTimeoutInSeconds" Value="{operationSeconds}" />"""));

            bool initialized = sut.InitializeSettingsFromConfig(sectionName);

            Assert.True(initialized);
            Assert.Equal(TimeSpan.FromSeconds(operationSeconds), sut.OperationTimeout);
        }
    }

    public sealed class KeepAliveTimeout: FabricTransportSettingsTest
    {
        [Fact]
        public void IsSetToGivenValue()
        {
            TimeSpan expected = fuzzy.TimeSpan();
            sut.KeepAliveTimeout = expected;
            Assert.Equal(expected, sut.KeepAliveTimeout);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class LoadFrom: FabricTransportSettingsTest
    {
        // Method parameters
        string sectionName;
        string filepath;
        string configPackageName;

        readonly string dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;

        public override void Dispose()
        {
            Directory.Delete(dir, recursive: true);
            base.Dispose();
        }

        [Fact]
        public void LoadsSettingsFromGivenSection()
        {
            sectionName = fuzzy.String().LettersOrDigits();
            int operationSeconds = fuzzy.Int32().Minimum(1);
            int keepAliveSeconds = fuzzy.Int32().Minimum(1);
            int connectMs = fuzzy.Int32().Minimum(1);
            long maxMessageSize = fuzzy.Int64();
            long maxQueueSize = fuzzy.Int64();
            long maxConcurrentCalls = fuzzy.Int64();
            filepath = CreateSettingsFile(dir, sectionName,
                $"""
                <Parameter Name="MaxMessageSize" Value="{maxMessageSize}" />
                <Parameter Name="MaxConcurrentCalls" Value="{maxConcurrentCalls}" />
                <Parameter Name="MaxQueueSize" Value="{maxQueueSize}" />
                <Parameter Name="OperationTimeoutInSeconds" Value="{operationSeconds}" />
                <Parameter Name="KeepAliveTimeoutInSeconds" Value="{keepAliveSeconds}" />
                <Parameter Name="ConnectTimeoutInMilliseconds" Value="{connectMs}" />
                <Parameter Name="SecurityCredentialsType" Value="X509" />
                """);

            var settings = FabricTransportSettings.LoadFrom(sectionName, filepath);

            Assert.Equal(TimeSpan.FromSeconds(operationSeconds), settings.OperationTimeout);
            Assert.Equal(TimeSpan.FromSeconds(keepAliveSeconds), settings.KeepAliveTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(connectMs), settings.ConnectTimeout);
            Assert.Equal(maxMessageSize, settings.MaxMessageSize);
            Assert.Equal(maxQueueSize, settings.MaxQueueSize);
            Assert.Equal(maxConcurrentCalls, settings.MaxConcurrentCalls);
            Assert.Equal(CredentialType.X509, settings.SecurityCredentials.CredentialType);
        }

        [Fact]
        public void LoadsWindowsCredentialsWithRemoteSpn()
        {
            sectionName = fuzzy.String().LettersOrDigits();
            string spn = "host/" + fuzzy.String().LettersOrDigits() + ".server.servicefabric.azure.test";
            filepath = CreateSettingsFile(dir, sectionName,
                $"""
                <Parameter Name="SecurityCredentialsType" Value="Windows" />
                <Parameter Name="RemoteSecurityPrincipalName" Value="{spn}" />
                """);

            var settings = FabricTransportSettings.LoadFrom(sectionName, filepath);

            Assert.Equal(CredentialType.Windows, settings.SecurityCredentials.CredentialType);
            var credentials = (WindowsCredentials)settings.SecurityCredentials;
            Assert.Equal(spn, credentials.RemoteSpn);
        }

        [Fact]
        public void LoadsNoneCredentialsWhenSecurityCredentialsTypeIsOmitted()
        {
            // The section is non-empty but omits SecurityCredentialsType to exercise the None fallback.
            sectionName = fuzzy.String().LettersOrDigits();
            filepath = CreateSettingsFile(dir, sectionName,
                """<Parameter Name="MaxConcurrentCalls" Value="16" />""");

            var settings = FabricTransportSettings.LoadFrom(sectionName, filepath);

            Assert.Equal(CredentialType.None, settings.SecurityCredentials.CredentialType);
        }

        [Fact]
        public void LoadsRichX509Credentials()
        {
            sectionName = fuzzy.String().LettersOrDigits();
            string findValue = fuzzy.String().LettersOrDigits();
            string findValueSecondary = fuzzy.String().LettersOrDigits();
            string storeName = fuzzy.String().LettersOrDigits();
            string remoteCommonName1 = fuzzy.String().LettersOrDigits();
            string remoteCommonName2 = fuzzy.String().LettersOrDigits();
            string remoteThumbprint1 = fuzzy.String().LettersOrDigits();
            string remoteThumbprint2 = fuzzy.String().LettersOrDigits();
            string issuerThumbprint = fuzzy.String().LettersOrDigits();
            string firstIssuerStore1 = fuzzy.String().LettersOrDigits();
            string firstIssuerStore2 = fuzzy.String().LettersOrDigits();
            string secondIssuerStore = fuzzy.String().LettersOrDigits();
            filepath = CreateSettingsFile(dir, sectionName,
                $"""
                <Parameter Name="SecurityCredentialsType" Value="X509" />
                <Parameter Name="CertificateFindType" Value="FindByThumbprint" />
                <Parameter Name="CertificateFindValue" Value="{findValue}" />
                <Parameter Name="CertificateFindValuebySecondary" Value="{findValueSecondary}" />
                <Parameter Name="CertificateProtectionLevel" Value="Sign" />
                <Parameter Name="CertificateStoreLocation" Value="LocalMachine" />
                <Parameter Name="CertificateStoreName" Value="{storeName}" />
                <Parameter Name="CertificateRemoteCommonNames" Value="{remoteCommonName1},{remoteCommonName2}" />
                <Parameter Name="CertificateRemoteThumbprints" Value="{remoteThumbprint1},{remoteThumbprint2}" />
                <Parameter Name="CertificateIssuerThumbprints" Value="{issuerThumbprint}" />
                <Parameter Name="CertificateApplicationIssuerStore/CN=FirstIssuer" Value="{firstIssuerStore1},{firstIssuerStore2}" />
                <Parameter Name="CertificateApplicationIssuerStore/CN=SecondIssuer" Value="{secondIssuerStore}" />
                """);

            var settings = FabricTransportSettings.LoadFrom(sectionName, filepath);

            Assert.Equal(CredentialType.X509, settings.SecurityCredentials.CredentialType);
            var credentials = (X509Credentials)settings.SecurityCredentials;
            Assert.Equal(X509FindType.FindByThumbprint, credentials.FindType);
            Assert.Equal(findValue, credentials.FindValue);
            Assert.Equal(findValueSecondary, credentials.FindValueSecondary);
            Assert.Equal(ProtectionLevel.Sign, credentials.ProtectionLevel);
            Assert.Equal(StoreLocation.LocalMachine, credentials.StoreLocation);
            Assert.Equal(storeName, credentials.StoreName);
            Assert.Equal([remoteCommonName1, remoteCommonName2], credentials.RemoteCommonNames);
            Assert.Equal([remoteThumbprint1, remoteThumbprint2], credentials.RemoteCertThumbprints);
            Assert.Equal([issuerThumbprint], credentials.IssuerThumbprints);
            // Order is not part of the contract: RemoteCertIssuers is populated from a Dictionary<string,string>
            // whose enumeration order is unspecified. Sort by Name to make the assertion deterministic.
            Assert.Collection(credentials.RemoteCertIssuers.OrderBy(i => i.Name, StringComparer.Ordinal),
                issuer =>
                {
                    Assert.Equal("CN=FirstIssuer", issuer.Name);
                    Assert.Equal([firstIssuerStore1, firstIssuerStore2], issuer.IssuerStores);
                },
                issuer =>
                {
                    Assert.Equal("CN=SecondIssuer", issuer.Name);
                    Assert.Equal([secondIssuerStore], issuer.IssuerStores);
                });
        }

        [Fact(Explicit = true)] // TODO: SUT bug. FabricTransportSettings.LoadFrom throws ArgumentException without a paramName.
        public void ThrowsArgumentExceptionWhenSectionDoesNotExistInSpecifiedFile()
        {
            // LoadFrom throws ArgumentException reporting the missing section, but constructs it without
            // a ParamName, so ex.ParamName is null instead of "sectionName".
            sectionName = "AbsentSection";
            filepath = CreateSettingsFile(dir, "PresentSection", "");
            var ex = Assert.Throws<ArgumentException>(() => FabricTransportSettings.LoadFrom(sectionName, filepath));
            Assert.Equal(nameof(sectionName), ex.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. FabricTransportSettings.LoadFrom throws ArgumentException without a paramName.
        public void ThrowsArgumentExceptionWhenSectionDoesNotExist()
        {
            // LoadFrom throws ArgumentException reporting the missing section, but constructs it without
            // a ParamName, so ex.ParamName is null instead of "sectionName".
            sectionName = fuzzy.String().LettersOrDigits();
            var ex = Assert.Throws<ArgumentException>(() => FabricTransportSettings.LoadFrom(sectionName));
            Assert.Equal(nameof(sectionName), ex.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. FabricTransportSettings.LoadFrom throws ArgumentException without a paramName.
        public void ThrowsArgumentExceptionWhenFileDoesNotExist()
        {
            // LoadFrom throws ArgumentException reporting the missing file, but constructs it without
            // a ParamName, so ex.ParamName is null instead of "filepath".
            sectionName = fuzzy.String().LettersOrDigits();
            filepath = Path.Combine(Path.GetTempPath(), fuzzy.String().LettersOrDigits(),
                fuzzy.String().LettersOrDigits() + ".xml");
            Assert.False(File.Exists(filepath), $"Pre-existing {filepath} would invalidate this test.");
            var ex = Assert.Throws<ArgumentException>(() => FabricTransportSettings.LoadFrom(sectionName, filepath));
            Assert.Equal(nameof(filepath), ex.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. FabricTransportSettings.LoadFrom throws ArgumentException without a paramName.
        public void ThrowsArgumentExceptionWhenConfigPackageDoesNotExist()
        {
            // LoadFrom throws ArgumentException reporting the missing config package, but constructs it
            // without a ParamName, so ex.ParamName is null instead of "configPackageName".
            sectionName = fuzzy.String().LettersOrDigits();
            configPackageName = fuzzy.String().LettersOrDigits();
            var ex = Assert.Throws<ArgumentException>(() => FabricTransportSettings.LoadFrom(sectionName, configPackageName: configPackageName));
            Assert.Equal(nameof(configPackageName), ex.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Requires a Service Fabric host process.
        public void LoadsTransportSettingsFromConfigPackageWhenConfigPackageNameIsSpecified() =>
            // The configPackageName branch calls InitializeConfigFileFromConfigPackage, which resolves the
            // package through FabricServiceConfig.InitializeFromConfigPackage -> FabricRuntime.GetActivationContext().
            // GetActivationContext only succeeds inside a Service Fabric host process, so the success path
            // is unreachable from a standalone test runner.
            throw new NotImplementedException();
    }

    public sealed class MaxConcurrentCalls: FabricTransportSettingsTest
    {
        [Fact]
        public void IsSetToGivenValue()
        {
            long expected = fuzzy.Int64();
            sut.MaxConcurrentCalls = expected;
            Assert.Equal(expected, sut.MaxConcurrentCalls);
        }
    }

    public sealed class MaxMessageSize: FabricTransportSettingsTest
    {
        [Fact]
        public void IsSetToGivenValue()
        {
            long expected = fuzzy.Int64();
            sut.MaxMessageSize = expected;
            Assert.Equal(expected, sut.MaxMessageSize);
        }
    }

    public sealed class MaxQueueSize: FabricTransportSettingsTest
    {
        [Fact]
        public void IsSetToGivenValue()
        {
            long expected = fuzzy.Int64();
            sut.MaxQueueSize = expected;
            Assert.Equal(expected, sut.MaxQueueSize);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class OnInitialize: FabricTransportSettingsTest
    {
        readonly string dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;

        public override void Dispose()
        {
            Directory.Delete(dir, recursive: true);
            base.Dispose();
        }

        [Fact]
        public void LoadsDefaultOperationTimeoutWhenOperationTimeoutIsOmitted()
        {
            FabricTransportSettings settings = LoadOmittingTestedSettings();
            settings.OperationTimeout = FabricTransportSettings.DefaultOperationTimeout + fuzzy.TimeSpan().Seconds().Minimum(TimeSpan.FromSeconds(1));

            settings.OnInitialize();

            Assert.Equal(FabricTransportSettings.DefaultOperationTimeout, settings.OperationTimeout);
        }

        [Fact]
        public void LoadsDefaultKeepAliveTimeoutWhenKeepAliveTimeoutIsOmitted()
        {
            FabricTransportSettings settings = LoadOmittingTestedSettings();
            settings.KeepAliveTimeout = FabricTransportSettings.DefaultKeepAliveTimeout + fuzzy.TimeSpan().Seconds().Minimum(TimeSpan.FromSeconds(1));

            settings.OnInitialize();

            Assert.Equal(FabricTransportSettings.DefaultKeepAliveTimeout, settings.KeepAliveTimeout);
        }

        [Fact]
        public void LoadsDefaultConnectTimeoutWhenConnectTimeoutIsOmitted()
        {
            FabricTransportSettings settings = LoadOmittingTestedSettings();
            settings.ConnectTimeout = FabricTransportSettings.DefaultConnectTimeout + fuzzy.TimeSpan().Milliseconds().Minimum(TimeSpan.FromMilliseconds(1));

            settings.OnInitialize();

            Assert.Equal(FabricTransportSettings.DefaultConnectTimeout, settings.ConnectTimeout);
        }

        [Fact]
        public void LoadsDefaultMaxMessageSizeWhenMaxMessageSizeIsOmitted()
        {
            FabricTransportSettings settings = LoadOmittingTestedSettings();
            settings.MaxMessageSize = FabricTransportSettings.DefaultMaxReceivedMessageSize + fuzzy.Int32().Minimum(1);

            settings.OnInitialize();

            Assert.Equal(FabricTransportSettings.DefaultMaxReceivedMessageSize, settings.MaxMessageSize);
        }

        // DefaultQueueSize (10000) and DefaultConcurrentCalls (0) are private, so these two tests assert the literal
        // defaults, matching Constructor.InitializesDefaults.

        [Fact]
        public void LoadsDefaultMaxQueueSizeWhenMaxQueueSizeIsOmitted()
        {
            FabricTransportSettings settings = LoadOmittingTestedSettings();
            settings.MaxQueueSize = 10000L + fuzzy.Int32().Minimum(1);

            settings.OnInitialize();

            Assert.Equal(10000, settings.MaxQueueSize);
        }

        [Fact]
        public void LoadsDefaultMaxConcurrentCallsWhenMaxConcurrentCallsIsOmitted()
        {
            FabricTransportSettings settings = LoadOmittingTestedSettings();
            settings.MaxConcurrentCalls = fuzzy.Int64().Minimum(1);

            settings.OnInitialize();

            Assert.Equal(0, settings.MaxConcurrentCalls);
        }

        FabricTransportSettings LoadOmittingTestedSettings()
        {
            // OnInitialize is internal virtual and is invoked by InitializeSettingsFromConfig, which is reached
            // through LoadFrom. The generated section omits every setting these tests assert on, so the loaded
            // ConfigSection exercises the fallback branches that substitute the Default* constants when the
            // corresponding parameter is absent. Each test pre-sets its setting to a value distinct from the asserted
            // default, then re-invokes OnInitialize directly, so observing the default afterwards proves it was
            // actually written rather than surviving untouched. RemoteSecurityPrincipalName is the non-asserted
            // filler that keeps the section non-empty without supplying any tested setting.

            // LoadFrom stays in the test body, not a constructor, because it would throw TypeInitializationException on
            // Linux before the WindowsOnlyAttribute can skip the test.

            string section = fuzzy.String().LettersOrDigits();
            string file = CreateSettingsFile(dir, section,
                """<Parameter Name="RemoteSecurityPrincipalName" Value="filler" />""");
            return FabricTransportSettings.LoadFrom(section, file);
        }
    }

    public sealed class OperationTimeout: FabricTransportSettingsTest
    {
        [Fact]
        public void IsSetToGivenValue()
        {
            TimeSpan expected = fuzzy.TimeSpan();
            sut.OperationTimeout = expected;
            Assert.Equal(expected, sut.OperationTimeout);
        }
    }

    public sealed class SecurityCredentials: FabricTransportSettingsTest
    {
        [Fact]
        public void IsSetToGivenValue()
        {
            WindowsCredentials expected = new();
            sut.SecurityCredentials = expected;
            Assert.Same(expected, sut.SecurityCredentials);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class ToNative: FabricTransportSettingsTest
    {
        readonly PinCollection pin = [];

        public ToNative()
        {
            // Suppress credentials marshalling; tests that exercise it reassign explicitly.
            sut.SecurityCredentials = null;
            sut.OperationTimeout = fuzzy.TimeSpan().Seconds();
            sut.KeepAliveTimeout = fuzzy.TimeSpan().Seconds();
            sut.ConnectTimeout = fuzzy.TimeSpan().Milliseconds();
            sut.MaxMessageSize = fuzzy.Int32().Minimum(0);
            sut.MaxConcurrentCalls = fuzzy.Int32().Minimum(1);
            sut.MaxQueueSize = fuzzy.Int32().Minimum(0);
        }

        public override void Dispose()
        {
            pin.Dispose();
            base.Dispose();
        }

        // FabricTransportSettings.ToNative marshals into NativeTypes.FABRIC_SERVICE_TRANSPORT_SETTINGS, which is
        // internal to System.Fabric. The byte layout matches FABRIC_TRANSPORT_SETTINGS (declared in this assembly),
        // so the test re-uses that struct to read back the marshaled values.

        [Fact]
        public void MarshalsScalarSettingsToNativeStruct()
        {
            // Drive each scalar from a known integer input so the assertion describes the intended int -> uint
            // marshalling instead of re-applying the SUT's own cast.
            int operationSeconds = fuzzy.Int32().Minimum(0);
            int keepAliveSeconds = fuzzy.Int32().Minimum(0);
            int maxMessageSize = fuzzy.Int32().Minimum(0);
            int maxConcurrentCalls = fuzzy.Int32().Minimum(1);
            int maxQueueSize = fuzzy.Int32().Minimum(0);
            sut.OperationTimeout = TimeSpan.FromSeconds(operationSeconds);
            sut.KeepAliveTimeout = TimeSpan.FromSeconds(keepAliveSeconds);
            sut.MaxMessageSize = maxMessageSize;
            sut.MaxConcurrentCalls = maxConcurrentCalls;
            sut.MaxQueueSize = maxQueueSize;

            IntPtr ptr = sut.ToNative(pin);

            var native = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS>(ptr);
            Assert.Equal(Convert.ToUInt32(operationSeconds), native.OperationTimeoutInSeconds);
            Assert.Equal(Convert.ToUInt32(keepAliveSeconds), native.KeepAliveTimeoutInSeconds);
            Assert.Equal(Convert.ToUInt32(maxMessageSize), native.MaxMessageSize);
            Assert.Equal(Convert.ToUInt32(maxConcurrentCalls), native.MaxConcurrentCalls);
            Assert.Equal(Convert.ToUInt32(maxQueueSize), native.MaxQueueSize);
        }

        [Fact]
        public void SetsSecurityCredentialsToZeroWhenNull()
        {
            sut.SecurityCredentials = null;

            IntPtr ptr = sut.ToNative(pin);

            var native = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS>(ptr);
            Assert.Equal(IntPtr.Zero, native.SecurityCredentials);
        }

        [Fact]
        public void ForwardsSecurityCredentialsToNativeStruct()
        {
            // WindowsCredentials marshals to FABRIC_SECURITY_CREDENTIALS with Kind = WINDOWS (2),
            // distinguishing it from the default NONE (0) kind so the assertion verifies that the
            // pointer actually points to the credentials produced by SecurityCredentials.ToNative.
            sut.SecurityCredentials = new WindowsCredentials();

            IntPtr ptr = sut.ToNative(pin);

            var native = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS>(ptr);
            Assert.NotEqual(IntPtr.Zero, native.SecurityCredentials);
            Assert.Equal((int)CredentialType.Windows, Marshal.ReadInt32(native.SecurityCredentials));
        }

        [Fact]
        public void ClampsOperationTimeoutToZeroWhenNegative()
        {
            sut.OperationTimeout = -fuzzy.TimeSpan().Seconds();

            IntPtr ptr = sut.ToNative(pin);

            var native = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS>(ptr);
            Assert.Equal(0u, native.OperationTimeoutInSeconds);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. FabricTransportSettings.ToNative does not validate OperationTimeout upper bound.
        public void ThrowsArgumentOutOfRangeExceptionWhenOperationTimeoutExceedsUInt32MaxSeconds()
        {
            // ToNative casts OperationTimeout.TotalSeconds directly to uint without range checking, so values
            // greater than uint.MaxValue silently overflow instead of throwing ArgumentOutOfRangeException with
            // ParamName "OperationTimeout".
            sut.OperationTimeout = TimeSpan.FromSeconds((double)uint.MaxValue + 1);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => sut.ToNative(pin));
            Assert.Equal(nameof(FabricTransportSettings.OperationTimeout), ex.ParamName);
        }

        [Fact]
        public void ClampsKeepAliveTimeoutToZeroWhenNegative()
        {
            sut.KeepAliveTimeout = -fuzzy.TimeSpan().Seconds();

            IntPtr ptr = sut.ToNative(pin);

            var native = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS>(ptr);
            Assert.Equal(0u, native.KeepAliveTimeoutInSeconds);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. FabricTransportSettings.ToNative does not validate KeepAliveTimeout upper bound.
        public void ThrowsArgumentOutOfRangeExceptionWhenKeepAliveTimeoutExceedsUInt32MaxSeconds()
        {
            // ToNative casts KeepAliveTimeout.TotalSeconds directly to uint without range checking, so values
            // greater than uint.MaxValue silently overflow instead of throwing ArgumentOutOfRangeException with
            // ParamName "KeepAliveTimeout".
            sut.KeepAliveTimeout = TimeSpan.FromSeconds((double)uint.MaxValue + 1);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => sut.ToNative(pin));
            Assert.Equal(nameof(FabricTransportSettings.KeepAliveTimeout), ex.ParamName);
        }

        [Fact]
        public void ThrowsArgumentOutOfRangeExceptionWhenMaxMessageSizeIsNegative()
        {
            sut.MaxMessageSize = fuzzy.Int64().Maximum(-1);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => sut.ToNative(pin));
            Assert.Equal(nameof(FabricTransportSettings.MaxMessageSize), ex.ParamName);
        }

        [Fact]
        public void ThrowsArgumentOutOfRangeExceptionWhenMaxMessageSizeExceedsInt32MaxValue()
        {
            sut.MaxMessageSize = fuzzy.Int64().Minimum((long)int.MaxValue + 1);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => sut.ToNative(pin));
            Assert.Equal(nameof(FabricTransportSettings.MaxMessageSize), ex.ParamName);
        }

        [Fact]
        public void ThrowsArgumentOutOfRangeExceptionWhenMaxConcurrentCallsIsNegative()
        {
            sut.MaxConcurrentCalls = fuzzy.Int64().Maximum(-1);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => sut.ToNative(pin));
            Assert.Equal(nameof(FabricTransportSettings.MaxConcurrentCalls), ex.ParamName);
        }

        [Fact]
        public void ThrowsArgumentOutOfRangeExceptionWhenMaxConcurrentCallsExceedsInt32MaxValue()
        {
            sut.MaxConcurrentCalls = fuzzy.Int64().Minimum((long)int.MaxValue + 1);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => sut.ToNative(pin));
            Assert.Equal(nameof(FabricTransportSettings.MaxConcurrentCalls), ex.ParamName);
        }

        [Fact]
        public void ThrowsArgumentOutOfRangeExceptionWhenMaxQueueSizeIsNegative()
        {
            sut.MaxQueueSize = fuzzy.Int64().Maximum(-1);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => sut.ToNative(pin));
            Assert.Equal(nameof(FabricTransportSettings.MaxQueueSize), ex.ParamName);
        }

        [Fact]
        public void ThrowsArgumentOutOfRangeExceptionWhenMaxQueueSizeExceedsInt32MaxValue()
        {
            sut.MaxQueueSize = fuzzy.Int64().Minimum((long)int.MaxValue + 1);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => sut.ToNative(pin));
            Assert.Equal(nameof(FabricTransportSettings.MaxQueueSize), ex.ParamName);
        }

        [Fact]
        public void SetsConnectTimeoutWhenPositive()
        {
            int connectMs = fuzzy.Int32().Minimum(1);
            sut.ConnectTimeout = TimeSpan.FromMilliseconds(connectMs);

            IntPtr ptr = sut.ToNative(pin);

            var native = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS>(ptr);
            var ex1 = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS_EX1>(native.Reserved);
            Assert.Equal(Convert.ToUInt32(connectMs), ex1.ConnectTimeoutInMilliseconds);
        }

        [Fact]
        public void UsesDefaultConnectTimeoutWhenNegative()
        {
            sut.ConnectTimeout = -fuzzy.TimeSpan().Milliseconds();

            IntPtr ptr = sut.ToNative(pin);

            var native = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS>(ptr);
            var ex1 = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS_EX1>(native.Reserved);
            Assert.Equal(Convert.ToUInt32(FabricTransportSettings.DefaultConnectTimeout.TotalMilliseconds), ex1.ConnectTimeoutInMilliseconds);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. FabricTransportSettings.ToNative does not validate ConnectTimeout upper bound.
        public void ThrowsArgumentOutOfRangeExceptionWhenConnectTimeoutExceedsUInt32MaxMilliseconds()
        {
            // ToNative casts ConnectTimeout.TotalMilliseconds directly to uint without range checking, so values
            // greater than uint.MaxValue silently overflow instead of throwing ArgumentOutOfRangeException with
            // ParamName "ConnectTimeout".
            sut.ConnectTimeout = TimeSpan.FromMilliseconds((double)uint.MaxValue + 1);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => sut.ToNative(pin));
            Assert.Equal(nameof(FabricTransportSettings.ConnectTimeout), ex.ParamName);
        }

        [Fact]
        public void EnablesMaxConcurrentCallsWhenGreaterThanZero()
        {
            sut.MaxConcurrentCalls = fuzzy.Int32().Minimum(1);

            IntPtr ptr = sut.ToNative(pin);

            var native = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS>(ptr);
            var ex1 = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS_EX1>(native.Reserved);
            var ex2 = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS_EX2>(ex1.Reserved);
            Assert.Equal(NativeTypes.ToBOOLEAN(true), ex2.EnableMaxConcurrentCalls);
        }

        [Fact]
        public void DisablesMaxConcurrentCallsWhenZero()
        {
            sut.MaxConcurrentCalls = 0;

            IntPtr ptr = sut.ToNative(pin);

            var native = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS>(ptr);
            var ex1 = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS_EX1>(native.Reserved);
            var ex2 = Marshal.PtrToStructure<FABRIC_TRANSPORT_SETTINGS_EX2>(ex1.Reserved);
            Assert.Equal(NativeTypes.ToBOOLEAN(false), ex2.EnableMaxConcurrentCalls);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. FabricTransportSettings.ToNative does not validate pin.
        public void ThrowsArgumentNullExceptionWhenPinIsNull()
        {
            // ToNative dereferences pin without validating it, producing NullReferenceException
            // instead of ArgumentNullException with ParamName "pin".
            var ex = Assert.Throws<ArgumentNullException>(() => sut.ToNative(null));
            Assert.Equal(nameof(pin), ex.ParamName);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class TryLoadFrom: FabricTransportSettingsTest
    {
        // Method parameters
        string sectionName;
        string filepath;
        string configPackageName;

        readonly string dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;

        public override void Dispose()
        {
            Directory.Delete(dir, recursive: true);
            base.Dispose();
        }

        [Fact]
        public void ReturnsTrueAndLoadsSettingsWhenSectionExists()
        {
            sectionName = fuzzy.String().LettersOrDigits();
            int operationSeconds = fuzzy.Int32().Minimum(1);
            filepath = CreateSettingsFile(dir, sectionName,
                $"""<Parameter Name="OperationTimeoutInSeconds" Value="{operationSeconds}" />""");

            bool succeeded = FabricTransportSettings.TryLoadFrom(sectionName, out var settings, filepath);

            Assert.True(succeeded);
            Assert.Equal(TimeSpan.FromSeconds(operationSeconds), settings.OperationTimeout);
        }

        [Fact]
        public void ReturnsFalseAndNullSettingsWhenSectionDoesNotExistInGivenFile()
        {
            sectionName = "AbsentSection";
            filepath = CreateSettingsFile(dir, "PresentSection", "");
            Assert.False(FabricTransportSettings.TryLoadFrom(sectionName, out var settings, filepath));
            Assert.Null(settings);
        }

        [Fact]
        public void ReturnsFalseAndNullSettingsWhenSectionDoesNotExist()
        {
            // With both filepath and configPackageName omitted, TryLoadFrom skips both init branches and
            // falls straight into InitializeSettingsFromConfig(sectionName), which returns false because the
            // randomly generated section name cannot exist in whatever FabricServiceConfig.GetConfig resolves to.
            sectionName = fuzzy.String().LettersOrDigits();
            Assert.False(FabricTransportSettings.TryLoadFrom(sectionName, out var settings));
            Assert.Null(settings);
        }

        [Fact]
        public void ReturnsFalseAndNullSettingsWhenFileDoesNotExist()
        {
            sectionName = fuzzy.String().LettersOrDigits();
            filepath = Path.Combine(Path.GetTempPath(), fuzzy.String().LettersOrDigits(),
                fuzzy.String().LettersOrDigits() + ".xml");
            Assert.False(File.Exists(filepath), $"Pre-existing {filepath} would invalidate this test.");

            Assert.False(FabricTransportSettings.TryLoadFrom(sectionName, out var settings, filepath));
            Assert.Null(settings);
        }

        [Fact]
        public void ReturnsFalseAndNullSettingsWhenConfigPackageDoesNotExist()
        {
            sectionName = fuzzy.String().LettersOrDigits();
            configPackageName = fuzzy.String().LettersOrDigits();
            Assert.False(FabricTransportSettings.TryLoadFrom(sectionName, out var settings, configPackageName: configPackageName));
            Assert.Null(settings);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Requires a Service Fabric host process.
        public void ReturnsTrueAndLoadsSettingsWhenConfigPackageExists() =>
            // The configPackageName branch calls InitializeConfigFileFromConfigPackage, which resolves the
            // package through FabricServiceConfig.InitializeFromConfigPackage -> FabricRuntime.GetActivationContext().
            // GetActivationContext only succeeds inside a Service Fabric host process, so the success path
            // is unreachable from a standalone test runner.
            throw new NotImplementedException();

        [Fact]
        public void ReturnsFalseAndNullSettingsWhenSettingValueIsInvalid()
        {
            // MaxMessageSize is parsed as long; a non-numeric value makes InitializeSettingsFromConfig throw,
            // exercising the catch-all branch of TryLoadFrom that swallows the exception and returns false.
            sectionName = fuzzy.String().LettersOrDigits();
            filepath = CreateSettingsFile(dir, sectionName, """<Parameter Name="MaxMessageSize" Value="not-a-long" />""");
            Assert.False(FabricTransportSettings.TryLoadFrom(sectionName, out var settings, filepath));
            Assert.Null(settings);
        }
    }

    static string CreateSettingsFile(string dir, string section, string parameters)
    {
        string path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".xml");
        File.WriteAllText(path,
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Settings xmlns="http://schemas.microsoft.com/2011/01/fabric">
              <Section Name="{section}">
                {parameters}
              </Section>
            </Settings>
            """);
        return path;
    }
}
