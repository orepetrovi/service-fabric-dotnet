// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;

namespace Microsoft.ServiceFabric;

public static class TestEnvironment
{
    public static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;
}
