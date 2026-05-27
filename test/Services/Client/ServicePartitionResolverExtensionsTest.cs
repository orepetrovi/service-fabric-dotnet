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
    public sealed class DisableNotification : ServicePartitionResolverExtensionsTest
    {
        readonly ServicePartitionResolver partitionResolver = new(Mock.Of<CreateFabricClientDelegate>());

        [Fact]
        public void SetsUseNotificationToFalse()
        {
            partitionResolver.DisableNotification();
            Assert.False(partitionResolver.UseNotification);
        }

        [Fact]
        public void ReturnsGivenResolver() =>
            Assert.Same(partitionResolver, partitionResolver.DisableNotification());

        // TODO: SUT should throw ArgumentNullException when partitionResolver is null; the extension
        // dereferences the null receiver, surfacing as NullReferenceException instead.
        [Fact]
        public void ThrowsNullReferenceExceptionWhenPartitionResolverIsNull() =>
            Assert.Throws<NullReferenceException>(() => ServicePartitionResolverExtensions.DisableNotification(null));
    }
}
