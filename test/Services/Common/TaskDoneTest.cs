// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Common;

public abstract class TaskDoneTest
{
    public sealed class Done : TaskDoneTest
    {
        [Fact]
        public void ReturnsCompletedTask() =>
            Assert.Equal(TaskStatus.RanToCompletion, TaskDone.Done.Status);

        [Fact]
        public void ReturnsSameInstanceAcrossCalls() =>
            Assert.Same(TaskDone.Done, TaskDone.Done);
    }
}
