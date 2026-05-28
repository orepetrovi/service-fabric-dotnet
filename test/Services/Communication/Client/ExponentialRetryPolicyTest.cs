// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.Client;

public abstract class ExponentialRetryPolicyTest
{
    readonly ExponentialRetryPolicy sut;

    // Constructor parameters
    readonly int defaultMaxRetryCount = fuzzy.Int32();
    readonly TimeSpan maxRetryJitter = fuzzy.TimeSpan()
        .Minimum(TimeSpan.FromMilliseconds(100))
        .Maximum(TimeSpan.FromMilliseconds(1000));
    readonly TimeSpan clientRetryTimeout = fuzzy.TimeSpan();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ExponentialRetryPolicyTest() =>
        sut = new ExponentialRetryPolicy(defaultMaxRetryCount, maxRetryJitter, clientRetryTimeout);

    public sealed class BaseRetryDelay : ExponentialRetryPolicyTest
    {
        [Fact]
        public void IsSetToGivenValue()
        {
            TimeSpan expected = fuzzy.TimeSpan();
            sut.BaseRetryDelay = expected;
            Assert.Equal(expected, sut.BaseRetryDelay);
        }
    }

    public sealed class Constructor_Int32_TimeSpan : ExponentialRetryPolicyTest
    {
        new readonly ExponentialRetryPolicy sut;

        public Constructor_Int32_TimeSpan() =>
            sut = new ExponentialRetryPolicy(defaultMaxRetryCount, clientRetryTimeout);

        [Fact]
        public void InitializesProperties()
        {
            Assert.Equal(defaultMaxRetryCount, sut.TotalNumberOfRetries);
            Assert.Equal(clientRetryTimeout, sut.ClientRetryTimeout);
            Assert.Equal(TimeSpan.FromSeconds(1), sut.BaseRetryDelay);
        }

        [Fact]
        public void UsesTwoSecondsAsMaxRetryJitter()
        {
            // Eliminate the base delay term so the observed delay is the jitter component alone.
            sut.BaseRetryDelay = TimeSpan.Zero;
            var parameters = new RetryDelayParameters(0, fuzzy.Boolean());
            var expectedMax = TimeSpan.FromSeconds(2);

            var observedMax = TimeSpan.Zero;
            for (int i = 0; i < Samples; i++)
            {
                TimeSpan delay = sut.GetNextRetryDelay(parameters);
                Assert.InRange(delay, TimeSpan.Zero, expectedMax);
                if (delay > observedMax)
                    observedMax = delay;
            }

            // Probability of every sample falling below half of the configured max with a uniform [0,1) scale is 2^-Samples.
            Assert.True(observedMax.Ticks > expectedMax.Ticks / 2);
        }
    }

    public sealed class Constructor_Int32_TimeSpan_TimeSpan : ExponentialRetryPolicyTest
    {
        [Fact]
        public void InitializesProperties()
        {
            Assert.Equal(defaultMaxRetryCount, sut.TotalNumberOfRetries);
            Assert.Equal(clientRetryTimeout, sut.ClientRetryTimeout);
            Assert.Equal(TimeSpan.FromSeconds(1), sut.BaseRetryDelay);
        }

        [Fact]
        public void UsesGivenMaxRetryJitter()
        {
            // Eliminate the base delay term so the observed delay is the jitter component alone.
            sut.BaseRetryDelay = TimeSpan.Zero;
            var parameters = new RetryDelayParameters(0, fuzzy.Boolean());

            var observedMax = TimeSpan.Zero;
            for (int i = 0; i < Samples; i++)
            {
                TimeSpan delay = sut.GetNextRetryDelay(parameters);
                Assert.InRange(delay, TimeSpan.Zero, maxRetryJitter);
                if (delay > observedMax)
                    observedMax = delay;
            }

            Assert.True(observedMax.Ticks > maxRetryJitter.Ticks / 2);
        }
    }

    public sealed class GetNextRetryDelay : ExponentialRetryPolicyTest
    {
        new readonly IRetryPolicy sut;

        // Method parameters
        RetryDelayParameters retryDelayParameters;

        readonly TimeSpan baseRetryDelay = fuzzy.TimeSpan()
            .Minimum(TimeSpan.FromMilliseconds(100))
            .Maximum(TimeSpan.FromMilliseconds(1000));

        public GetNextRetryDelay()
        {
            base.sut.BaseRetryDelay = baseRetryDelay;
            sut = base.sut;
        }

        [Fact]
        public void ReturnsBaseDelayPlusJitterWhenRetryAttemptIsBelowSameDelayRequestCounter()
        {
            int retryAttempt = fuzzy.Int32().Between(0, ExponentialRetryPolicy.SameDelayRequestCounter - 1);
            AssertDelayWithinExpectedRange(retryAttempt, delayMultiplier: 0);
        }

        [Fact]
        public void ShiftsBaseDelayByDelayMultiplierWhenRetryAttemptIsMultipleOfSameDelayRequestCounter()
        {
            int delayMultiplier = fuzzy.Int32().Between(1, ExponentialRetryPolicy.MaxDelayMultiplier - 1);
            int retryAttempt = delayMultiplier * ExponentialRetryPolicy.SameDelayRequestCounter;
            AssertDelayWithinExpectedRange(retryAttempt, delayMultiplier);
        }

        [Fact]
        public void UsesSameBaseDelayForSameDelayRequestCounterConsecutiveAttempts()
        {
            int delayMultiplier = fuzzy.Int32().Between(0, ExponentialRetryPolicy.MaxDelayMultiplier - 1);
            int firstAttempt = delayMultiplier * ExponentialRetryPolicy.SameDelayRequestCounter;
            int lastAttempt = firstAttempt + ExponentialRetryPolicy.SameDelayRequestCounter - 1;
            AssertDelayWithinExpectedRange(firstAttempt, delayMultiplier);
            AssertDelayWithinExpectedRange(lastAttempt, delayMultiplier);
        }

        [Fact]
        public void CapsDelayMultiplierAtMaxDelayMultiplier()
        {
            int extraAttempts = fuzzy.Int32().Between(1, 100);
            int retryAttempt = (ExponentialRetryPolicy.MaxDelayMultiplier + extraAttempts)
                * ExponentialRetryPolicy.SameDelayRequestCounter;
            AssertDelayWithinExpectedRange(retryAttempt, ExponentialRetryPolicy.MaxDelayMultiplier);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation for retryDelayParameters.
        public void ThrowsArgumentNullExceptionWhenRetryDelayParametersIsNull() =>
            _ = Assert.Throws<ArgumentNullException>(() => sut.GetNextRetryDelay(null));

        void AssertDelayWithinExpectedRange(int retryAttempt, int delayMultiplier)
        {
            retryDelayParameters = new RetryDelayParameters(retryAttempt, fuzzy.Boolean());

            long expectedBaseMs = (long)((int)baseRetryDelay.TotalMilliseconds << delayMultiplier);
            var expectedMin = TimeSpan.FromMilliseconds(expectedBaseMs);
            var expectedMax = TimeSpan.FromMilliseconds(expectedBaseMs + maxRetryJitter.TotalMilliseconds);

            var observedMin = TimeSpan.MaxValue;
            var observedMax = TimeSpan.Zero;
            for (int i = 0; i < Samples; i++)
            {
                TimeSpan delay = sut.GetNextRetryDelay(retryDelayParameters);
                Assert.InRange(delay, expectedMin, expectedMax);
                if (delay < observedMin)
                    observedMin = delay;
                if (delay > observedMax)
                    observedMax = delay;
            }

            // Both the constant base term and the random jitter term must contribute to the observed delay.
            Assert.True(observedMin.Ticks >= expectedMin.Ticks);
            Assert.True((observedMax - observedMin).Ticks > maxRetryJitter.Ticks / 2);
        }
    }

    public sealed class MaxDelayMultiplier : ExponentialRetryPolicyTest
    {
        [Fact]
        public void DefaultIsNine() =>
            Assert.Equal(9, ExponentialRetryPolicy.MaxDelayMultiplier);

        [Fact]
        public void CapsDelayMultiplierUsedByGetNextRetryDelay()
        {
            int original = ExponentialRetryPolicy.MaxDelayMultiplier;
            try
            {
                int newMax = fuzzy.Int32().Between(1, 5);
                ExponentialRetryPolicy.MaxDelayMultiplier = newMax;
                sut.BaseRetryDelay = TimeSpan.FromMilliseconds(1);
                int retryAttempt = (newMax + fuzzy.Int32().Between(1, 10))
                    * ExponentialRetryPolicy.SameDelayRequestCounter;
                long expectedBaseMs = 1L << newMax;
                var expectedMin = TimeSpan.FromMilliseconds(expectedBaseMs);
                var expectedMax = TimeSpan.FromMilliseconds(expectedBaseMs + maxRetryJitter.TotalMilliseconds);
                TimeSpan delay = sut.GetNextRetryDelay(new RetryDelayParameters(retryAttempt, fuzzy.Boolean()));
                Assert.InRange(delay, expectedMin, expectedMax);
            }
            finally
            {
                ExponentialRetryPolicy.MaxDelayMultiplier = original;
            }
        }
    }

    public sealed class SameDelayRequestCounter : ExponentialRetryPolicyTest
    {
        [Fact]
        public void DefaultIsThree() =>
            Assert.Equal(3, ExponentialRetryPolicy.SameDelayRequestCounter);

        [Fact]
        public void DeterminesGroupingUsedByGetNextRetryDelay()
        {
            int original = ExponentialRetryPolicy.SameDelayRequestCounter;
            try
            {
                int newCounter = fuzzy.Int32().Between(2, 10);
                ExponentialRetryPolicy.SameDelayRequestCounter = newCounter;
                sut.BaseRetryDelay = TimeSpan.FromMilliseconds(1);
                int delayMultiplier = fuzzy.Int32().Between(1, ExponentialRetryPolicy.MaxDelayMultiplier - 1);
                int retryAttempt = delayMultiplier * newCounter;
                long expectedBaseMs = 1L << delayMultiplier;
                var expectedMin = TimeSpan.FromMilliseconds(expectedBaseMs);
                var expectedMax = TimeSpan.FromMilliseconds(expectedBaseMs + maxRetryJitter.TotalMilliseconds);
                TimeSpan delay = sut.GetNextRetryDelay(new RetryDelayParameters(retryAttempt, fuzzy.Boolean()));
                Assert.InRange(delay, expectedMin, expectedMax);
            }
            finally
            {
                ExponentialRetryPolicy.SameDelayRequestCounter = original;
            }
        }
    }

    const int Samples = 100;
}
