// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.Client;

public abstract class UtilityTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    public sealed class ShouldRetryOperation : UtilityTest
    {
        // Method parameters
        readonly string currentExceptionId = fuzzy.String();
        readonly int maxRetryCount = fuzzy.Int32().Between(2, int.MaxValue - 1);
        string lastSeenExceptionId;
        int currentRetryCount = fuzzy.Int32();

        public ShouldRetryOperation() =>
            lastSeenExceptionId = fuzzy.String() + currentExceptionId; // different by construction

        [Fact]
        public void ReturnsTrueAndIncrementsCurrentRetryCountWhenCurrentExceptionIdEqualsLastSeenExceptionIdAndCurrentRetryCountIsBelowMaxRetryCount()
        {
            lastSeenExceptionId = currentExceptionId;
            currentRetryCount = maxRetryCount - 1;

            bool result = Utility.ShouldRetryOperation(
                currentExceptionId, maxRetryCount, ref lastSeenExceptionId, ref currentRetryCount);

            Assert.True(result);
            Assert.Equal(currentExceptionId, lastSeenExceptionId);
            Assert.Equal(maxRetryCount, currentRetryCount);
        }

        [Fact]
        public void ReturnsFalseWithoutModifyingRefsWhenMaxRetryCountIsZero()
        {
            string lastSeenExceptionIdBefore = lastSeenExceptionId;
            int currentRetryCountBefore = currentRetryCount;

            bool result = Utility.ShouldRetryOperation(
                currentExceptionId, 0, ref lastSeenExceptionId, ref currentRetryCount);

            Assert.False(result);
            Assert.Equal(lastSeenExceptionIdBefore, lastSeenExceptionId);
            Assert.Equal(currentRetryCountBefore, currentRetryCount);
        }

        [Fact]
        public void ReturnsFalseWithoutModifyingRefsWhenCurrentExceptionIdEqualsLastSeenExceptionIdAndCurrentRetryCountEqualsMaxRetryCount()
        {
            lastSeenExceptionId = currentExceptionId;
            currentRetryCount = maxRetryCount;

            bool result = Utility.ShouldRetryOperation(
                currentExceptionId, maxRetryCount, ref lastSeenExceptionId, ref currentRetryCount);

            Assert.False(result);
            Assert.Equal(currentExceptionId, lastSeenExceptionId);
            Assert.Equal(maxRetryCount, currentRetryCount);
        }

        [Fact]
        public void ReturnsFalseWithoutModifyingRefsWhenCurrentExceptionIdEqualsLastSeenExceptionIdAndCurrentRetryCountExceedsMaxRetryCount()
        {
            lastSeenExceptionId = currentExceptionId;
            currentRetryCount = maxRetryCount + 1;

            bool result = Utility.ShouldRetryOperation(
                currentExceptionId, maxRetryCount, ref lastSeenExceptionId, ref currentRetryCount);

            Assert.False(result);
            Assert.Equal(currentExceptionId, lastSeenExceptionId);
            Assert.Equal(maxRetryCount + 1, currentRetryCount);
        }

        [Fact]
        public void ReturnsTrueAndUpdatesLastSeenExceptionIdAndResetsCurrentRetryCountToOneWhenCurrentExceptionIdDiffersFromLastSeenExceptionId()
        {
            bool result = Utility.ShouldRetryOperation(
                currentExceptionId, maxRetryCount, ref lastSeenExceptionId, ref currentRetryCount);

            Assert.True(result);
            Assert.Equal(currentExceptionId, lastSeenExceptionId);
            Assert.Equal(1, currentRetryCount);
        }
    }
}
