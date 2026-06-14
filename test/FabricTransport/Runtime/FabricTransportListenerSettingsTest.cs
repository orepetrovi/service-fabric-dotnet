// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.ObjectModel;
using System.Fabric;
using System.Fabric.Description;
using Fuzzy;
using Inspector;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.FabricTransport.Runtime;

[Collection(nameof(FabricServiceConfigSingleton))]
public abstract class FabricTransportListenerSettingsTest
{
    readonly FabricTransportListenerSettings sut = new();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    public sealed class Constructor : FabricTransportListenerSettingsTest
    {
        [Fact]
        public void InitializesEndpointResourceName() =>
            Assert.Equal("ServiceEndpoint", sut.EndpointResourceName);
    }

    public sealed class EndpointResourceName : FabricTransportListenerSettingsTest
    {
        [Fact]
        public void IsSetToGivenValue()
        {
            string expected = fuzzy.String();
            sut.EndpointResourceName = expected;
            Assert.Same(expected, sut.EndpointResourceName);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class GetDefault : FabricTransportListenerSettingsTest
    {
        readonly string sectionName = fuzzy.String();

        public GetDefault() => ResetFabricServiceConfig();

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Requires a Service Fabric host process.
        public void ReturnsListenerSettingsLoadedFromConfigWhenSectionIsPresent() =>
            // GetDefault delegates to TryLoadFrom without a file fallback, which only succeeds when
            // FabricRuntime.GetActivationContext() resolves a config package - i.e. inside an SF host.
            throw new NotImplementedException();

        [Fact]
        public void ReturnsListenerSettingsWithDefaultEndpointResourceNameWhenTryLoadFromFails()
        {
            var settings = FabricTransportListenerSettings.GetDefault(sectionName);

            Assert.Equal("ServiceEndpoint", settings.EndpointResourceName);
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class GetListenerAddress : FabricTransportListenerSettingsTest
    {
        // Method parameters
        readonly ServiceContext serviceContext = fuzzy.ServiceContext();

        readonly EndpointResourceDescriptionCollection endpoints = [];

        public GetListenerAddress()
        {
            _ = Mock.Get(serviceContext.CodePackageActivationContext).Setup(_ => _.GetEndpoints()).Returns(endpoints);
            sut.EndpointResourceName = fuzzy.String();
        }

        [Fact]
        public void ReturnsListenerAddressWithPortOfEndpointMatchingEndpointResourceName()
        {
            // Non-zero distinguishes a matched port from GetEndpointPort's not-found 0 sentinel.
            int expectedPort = fuzzy.Int32().Minimum(1);
            endpoints.Add(CreateEndpoint(sut.EndpointResourceName, expectedPort));

            FabricTransportListenerAddress address = sut.GetListenerAddress(serviceContext);

            Assert.Same(serviceContext.ListenAddress, address.IpAddressOrFQDN);
            Assert.Equal(expectedPort, address.Port);
            AssertPathFormat(address.Path);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. GetListenerAddress does not validate serviceContext.
        public void ThrowsArgumentNullExceptionWhenServiceContextIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => sut.GetListenerAddress(serviceContext: null));
            Assert.Equal(nameof(serviceContext), exception.ParamName);
        }

        void AssertPathFormat(string path)
        {
            string expectedPrefix = FormattableString.Invariant($"{serviceContext.PartitionId}-{serviceContext.ReplicaOrInstanceId}-");
            Assert.StartsWith(expectedPrefix, path);
#if NET
            _ = Guid.Parse(path[expectedPrefix.Length..]);
#else
            _ = Guid.Parse(path.Substring(expectedPrefix.Length));
#endif
        }

        static EndpointResourceDescription CreateEndpoint(string name, int port)
        {
            EndpointResourceDescription endpoint = new() { Name = name };
            endpoint.Property<int>().Set(port);
            return endpoint;
        }

        sealed class EndpointResourceDescriptionCollection : KeyedCollection<string, EndpointResourceDescription>
        {
            protected override string GetKeyForItem(EndpointResourceDescription item) => item.Name;
        }
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class LoadFrom : FabricTransportListenerSettingsTest
    {
        readonly string sectionName = fuzzy.String();
        readonly string configPackageName = fuzzy.String();

        public LoadFrom() => ResetFabricServiceConfig();

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Requires a Service Fabric host process.
        public void LoadsListenerSettingsFromConfigPackageWhenConfigPackageNameIsSpecified() =>
            // LoadFrom calls InitializeConfigFileFromConfigPackage, which resolves the package via
            // FabricServiceConfig.InitializeFromConfigPackage -> FabricRuntime.GetActivationContext().
            // GetActivationContext succeeds only inside a Service Fabric host process.
            throw new NotImplementedException();

        [Fact]
        public void ThrowsArgumentExceptionWhenSpecifiedConfigPackageCannotBeLoaded() =>
            Assert.Throws<ArgumentException>(() =>
                FabricTransportListenerSettings.LoadFrom(sectionName, configPackageName));

        [Fact(Explicit = true)] // TODO: SUT bug. LoadFrom builds the ArgumentException without a paramName, so ParamName is null instead of "configPackageName".
        public void ReportsConfigPackageNameAsParamNameWhenSpecifiedConfigPackageCannotBeLoaded()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                FabricTransportListenerSettings.LoadFrom(sectionName, configPackageName));
            Assert.Equal(nameof(configPackageName), exception.ParamName);
        }

        [Fact]
        public void ThrowsArgumentExceptionWhenDefaultConfigPackageCannotBeLoaded() =>
            Assert.Throws<ArgumentException>(() =>
                FabricTransportListenerSettings.LoadFrom(sectionName));

        [Fact(Explicit = true)] // TODO: SUT bug. LoadFrom builds the ArgumentException without a paramName, so ParamName is null instead of "configPackageName".
        public void ReportsConfigPackageNameAsParamNameWhenDefaultConfigPackageCannotBeLoaded()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                FabricTransportListenerSettings.LoadFrom(sectionName));
            Assert.Equal(nameof(configPackageName), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Requires a Service Fabric host process.
        public void ThrowsArgumentExceptionWhenSectionCannotBeFound() =>
            // Reaching the section-not-found branch requires InitializeConfigFileFromConfigPackage to
            // succeed first, which resolves the config package via FabricRuntime.GetActivationContext()
            // - only available inside a Service Fabric host process.
            throw new NotImplementedException();
    }

    [WindowsOnly("Can't load libFabricCommon.so on Linux.")]
    public sealed class TryLoadFrom : FabricTransportListenerSettingsTest
    {
        readonly string sectionName = fuzzy.String();
        readonly string configPackageName = fuzzy.String();

        public TryLoadFrom() => ResetFabricServiceConfig();

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Requires a Service Fabric host process.
        public void ReturnsTrueAndListenerSettingsWhenConfigPackageNameIsSpecified() =>
            // TryLoadFrom calls InitializeConfigFileFromConfigPackage, which resolves the named package via
            // FabricServiceConfig.InitializeFromConfigPackage -> FabricRuntime.GetActivationContext().
            // GetActivationContext succeeds only inside a Service Fabric host process.
            throw new NotImplementedException();

        [Fact]
        public void ReturnsFalseAndNullWhenSpecifiedConfigPackageCannotBeLoaded()
        {
            bool result = FabricTransportListenerSettings.TryLoadFrom(
                sectionName, out FabricTransportListenerSettings listenerSettings, configPackageName);

            Assert.False(result);
            Assert.Null(listenerSettings);
        }

        [Fact]
        public void ReturnsFalseAndNullWhenDefaultConfigPackageCannotBeLoaded()
        {
            bool result = FabricTransportListenerSettings.TryLoadFrom(
                sectionName, out FabricTransportListenerSettings listenerSettings);

            Assert.False(result);
            Assert.Null(listenerSettings);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Requires a Service Fabric host process.
        public void ReturnsFalseAndNullWhenSectionCannotBeFound() =>
            // Reaching the section-not-found branch requires InitializeConfigFileFromConfigPackage to
            // succeed first, which resolves the config package via FabricRuntime.GetActivationContext()
            // - only available inside a Service Fabric host process.
            throw new NotImplementedException();

        [Fact(Explicit = true)] // TODO: SUT testability limitation. Requires a Service Fabric host process.
        public void ReturnsFalseAndNullWhenInitializationThrows() =>
            // Exercises the catch (Exception) branch in TryLoadFrom. Triggering it requires
            // InitializeConfigFileFromConfigPackage or InitializeSettingsFromConfig to throw, both of which
            // route through FabricServiceConfig / FabricRuntime.GetActivationContext() with no mockable seam.
            throw new NotImplementedException();
    }

    // Reset the process-wide FabricServiceConfig singleton so each test loads configuration from scratch.
    // Tests in this collection share the singleton; a previous test's instance would otherwise leak state
    // across tests and make outcomes depend on execution order.
    static void ResetFabricServiceConfig() => typeof(FabricServiceConfig).Field<FabricServiceConfig>().Set(null);
}
