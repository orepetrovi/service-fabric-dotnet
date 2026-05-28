// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using Inspector;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.Client;

public abstract class RandomGeneratorTest
{
    readonly RandomGenerator sut = new();

    public sealed class Constructor : RandomGeneratorTest
    {
        [Fact]
        public void InitializesRandomLock() =>
            Assert.NotNull(sut.Field<object>().Value);

        [Fact]
        public void InitializesRand() =>
            Assert.NotNull(sut.Field<Random>().Value);
    }

    public sealed class NextDouble : RandomGeneratorTest
    {
        const int Samples = 100;

        [Fact]
        public void ReturnsValueGreaterThanOrEqualToZeroAndLessThanOne()
        {
            for (int i = 0; i < Samples; i++)
            {
                double value = sut.NextDouble();
                Assert.True(value >= 0.0 && value < 1.0, $"Expected [0.0, 1.0) but got {value}.");
            }
        }
    }
}
