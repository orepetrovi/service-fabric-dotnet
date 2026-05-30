// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Fabric.Management.ServiceModel;
using System.IO;
using Xunit;

namespace Microsoft.ServiceFabric.FabricTransport;

[WindowsOnly("Can't load libFabricCommon.so on Linux.")]
public abstract class SettingsConfigParserTest
{
    readonly IFabricServiceConfigParser sut = new SettingsConfigParser();

    public sealed class Parse : SettingsConfigParserTest
    {
        readonly string fileName = Path.Combine(AppContext.BaseDirectory, "ServiceCommunicationTestSettings.xml");

        [Fact]
        public void ReturnsSettingsTypeParsedFromFile()
        {
            SettingsType actual = sut.Parse(fileName);

            SettingsTypeSection section = Assert.Single(actual.Section);
            Assert.Equal("TestServiceListenerTransportSettings", section.Name);
        }
    }
}
