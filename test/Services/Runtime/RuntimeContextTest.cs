// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using Inspector;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Runtime;

public abstract class RuntimeContextTest
{
    readonly RuntimeContext sut = new();

    public sealed class Dispose : RuntimeContextTest
    {
        [Fact]
        public void DoesNotThrowWhenRuntimeAndCodePackageContextAreNull() =>
            sut.Dispose();

        [Fact]
        public void DisposesCodePackageContext()
        {
            var codePackageContext = new Mock<ICodePackageActivationContext>();
            sut.Property<ICodePackageActivationContext>().Set(codePackageContext.Object);

            sut.Dispose();

            codePackageContext.Verify(_ => _.Dispose(), Times.Once);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. FabricRuntime is sealed with no mockable seam.
        public void DisposesRuntime() =>
            throw new NotImplementedException(
                "RuntimeContext.Runtime is a sealed System.Fabric.FabricRuntime with no mockable seam, " +
                "so the non-null branch of Runtime?.Dispose() in RuntimeContext.Dispose() cannot be covered " +
                "without testability changes to the SUT.");
    }

    public sealed class GetOrCreateAsync : RuntimeContextTest
    {
        [Fact(Explicit = true)] // TODO: SUT testability limitation. FabricRuntime is sealed with no mockable seam.
        public void ReturnsSharedContext() =>
            throw new NotImplementedException(
                "RuntimeContext.GetOrCreateAsync calls static FabricRuntime.GetNodeContextAsync, " +
                "GetActivationContextAsync, and CreateAsync. FabricRuntime is sealed in System.Fabric with no " +
                "mockable seam, so the double-checked locking, exception cleanup, and shared-instance disposal " +
                "of losers cannot be covered without testability changes to the SUT.");
    }
}
