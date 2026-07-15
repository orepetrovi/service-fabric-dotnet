// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Common;

public abstract class TaskDoneOfTTest
{
    public sealed class Done : TaskDoneOfTTest
    {
        [Fact]
        public void ReturnsCompletedTask() =>
            Assert.Equal(TaskStatus.RanToCompletion, TaskDone<int>.Done.Status);

        [Fact]
        public async Task ReturnsDefaultValueOfValueType() =>
            Assert.Equal(0, await TaskDone<int>.Done);

        [Fact]
        public async Task ReturnsDefaultValueOfReferenceType() =>
            Assert.Null(await TaskDone<string>.Done);

        [Fact]
        public void ReturnsSameInstanceAcrossCalls() =>
            Assert.Same(TaskDone<int>.Done, TaskDone<int>.Done);
    }
}
