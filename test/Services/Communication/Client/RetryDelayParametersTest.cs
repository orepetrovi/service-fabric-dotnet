// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.Client;

public abstract class RetryDelayParametersTest
{
    readonly RetryDelayParameters sut;

    // Constructor parameters
    readonly int retryAttempt = fuzzy.Int32();
    readonly bool isTransient = fuzzy.Boolean();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    RetryDelayParametersTest() =>
        sut = new RetryDelayParameters(retryAttempt, isTransient);

    public sealed class IsTransient : RetryDelayParametersTest
    {
        [Fact]
        public void ReturnsValueGivenToConstructor() =>
            Assert.Equal(isTransient, sut.IsTransient);
    }

    public sealed class RetryAttempt : RetryDelayParametersTest
    {
        [Fact]
        public void ReturnsValueGivenToConstructor() =>
            Assert.Equal(retryAttempt, sut.RetryAttempt);
    }
}
