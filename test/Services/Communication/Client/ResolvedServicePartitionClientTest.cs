// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using Inspector;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.Client;

public abstract class ResolvedServicePartitionClientTest
{
    readonly ResolvedServicePartitionClient sut = new();

    public sealed class Constructor : ResolvedServicePartitionClientTest
    {
        [Fact]
        public void InitializesProperties()
        {
            Assert.Null(sut.Rsp);
            Assert.Null(sut.Client);
        }
    }

    public sealed class Rsp : ResolvedServicePartitionClientTest
    {
        [Fact]
        public void ReturnsValuePreviouslySet()
        {
            ResolvedServicePartition rsp = MakeRsp();
            sut.Rsp = rsp;
            Assert.Same(rsp, sut.Rsp);
        }

        [Fact]
        public void IsNullAfterSettingToNull()
        {
            sut.Rsp = MakeRsp();
            sut.Rsp = null;
            Assert.Null(sut.Rsp);
        }
    }

    public sealed class Client : ResolvedServicePartitionClientTest
    {
        readonly ICommunicationClient client = Mock.Of<ICommunicationClient>();

        [Fact]
        public void ReturnsValuePreviouslySet()
        {
            sut.Client = client;
            Assert.Same(client, sut.Client);
        }

        [Fact]
        public void IsNullAfterSettingToNull()
        {
            sut.Client = client;
            sut.Client = null;
            Assert.Null(sut.Client);
        }
    }

    public sealed class Constructor_ResolvedServicePartitionClient : ResolvedServicePartitionClientTest
    {
        readonly ResolvedServicePartitionClient other = new()
        {
            Rsp = MakeRsp(),
            Client = Mock.Of<ICommunicationClient>(),
        };

        [Fact]
        public void CopiesRspAndClientFromOther()
        {
            var copy = new ResolvedServicePartitionClient(other);

            Assert.Same(other.Rsp, copy.Rsp);
            Assert.Same(other.Client, copy.Client);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Copy constructor should throw ArgumentNullException when other is null
        public void ThrowsArgumentNullExceptionWhenOtherIsNull()
        {
            var thrown = Assert.Throws<ArgumentNullException>(() => new ResolvedServicePartitionClient(null));
            Assert.Equal("other", thrown.ParamName);
        }
    }

    static ResolvedServicePartition MakeRsp()
    {
        var rsp = Type<ResolvedServicePartition>.Uninitialized();
        rsp.Property<ServicePartitionInformation>().Set(Type<SingletonPartitionInformation>.Uninitialized());
        return rsp;
    }
}
