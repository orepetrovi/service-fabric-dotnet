// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using Xunit;

namespace Microsoft.ServiceFabric.FabricTransport;

// Serializes tests that mutate the FabricServiceConfig.instance singleton or share EntrySettingsFile.Path.
// xUnit v3 runs distinct test classes as independent collections in parallel by default.
[CollectionDefinition(nameof(FabricServiceConfigSingleton), DisableParallelization = true)]
public sealed class FabricServiceConfigSingleton;
