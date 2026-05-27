// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Reflection;

namespace Microsoft.ServiceFabric.Services.Communication.AspNetCore;

static class InspectorWorkarounds
{
    // TODO: Inspector v0.9.0 sut.Constructor<TSig>() binds multiple overloads when delegate-typed parameters
    // only differ in generic arguments (relaxed signature matching). Track via olegsych/inspector once filed.
    public static ConstructorInfo Constructor<T>(params Type[] parameterTypes) =>
        typeof(T).GetConstructor(parameterTypes)!;
}
