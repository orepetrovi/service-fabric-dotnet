// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using Microsoft.ServiceFabric.FabricTransport.Runtime;
using Xunit;

namespace Microsoft.ServiceFabric.FabricTransport;

public abstract class FabricTransportListenerSettingsTest
{
    const string onLinux = "Can't load FabricCommon.dll";

    public sealed class LoadFrom: FabricTransportListenerSettingsTest
    {
        [Fact(Skip=onLinux, SkipUnless=nameof(TestEnvironment.IsWindows), SkipType=typeof(TestEnvironment))]
        public void ThrowsExceptionWhenCodePackageDoesNotExist() => 
            Assert.Throws<ArgumentException>(() => FabricTransportListenerSettings.LoadFrom("TestServiceListener", "Config1"));

        [Fact(Skip=onLinux, SkipUnless=nameof(TestEnvironment.IsWindows), SkipType=typeof(TestEnvironment))]
        public static void ThrowsExceptionWhenSectionDoesNotExist() =>
            Assert.Throws<ArgumentException>(() => FabricTransportListenerSettings.LoadFrom("TestServiceListener"));
    }

    public sealed class TryLoadFrom: FabricTransportListenerSettingsTest
    {
        [Fact(Skip=onLinux, SkipUnless=nameof(TestEnvironment.IsWindows), SkipType=typeof(TestEnvironment))]
        public void ReturnsFalseWhenConfigurationPackageDoesNotExist() =>
            Assert.False(FabricTransportListenerSettings.TryLoadFrom("TestServiceListenerTransportSettings", out _, "Config2"));
    }
}