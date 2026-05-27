// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Client;

public abstract class ServicePartitionResolverExtensionsTest
{
    readonly ServicePartitionResolver sut = new(Mock.Of<CreateFabricClientDelegate>());

    public sealed class DisableNotification : ServicePartitionResolverExtensionsTest
    {
        [Fact]
        public void SetsUseNotificationToFalse()
        {
            sut.DisableNotification();
            Assert.False(sut.UseNotification);
        }

        [Fact]
        public void ReturnsGivenResolver() =>
            Assert.Same(sut, sut.DisableNotification());

        // TODO: SUT should throw ArgumentNullException when partitionResolver is null; the extension
        // dereferences the null receiver, surfacing as NullReferenceException instead.
        [Fact]
        public void ThrowsNullReferenceExceptionWhenResolverIsNull() =>
            Assert.Throws<NullReferenceException>(() => ServicePartitionResolverExtensions.DisableNotification(null));
    }
}
