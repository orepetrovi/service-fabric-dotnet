// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Runtime.Serialization;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Common;

public abstract class SerializationUtilityTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    static readonly DataContractSerializer Serializer = new(typeof(Payload));

    public sealed class Deserialize : SerializationUtilityTest
    {
        readonly DataContractSerializer serializer = Serializer;
        readonly byte[] buffer;

        readonly Payload msg = new() { Name = fuzzy.String(), Value = fuzzy.Int32() };

        public Deserialize() =>
            buffer = SerializationUtility.Serialize(serializer, msg);

        [Fact]
        public void ReturnsMsgFromBinaryXmlEncoding()
        {
            Payload result = (Payload)SerializationUtility.Deserialize(serializer, buffer);

            Assert.Equal(msg.Name, result.Name);
            Assert.Equal(msg.Value, result.Value);
        }

        [Fact]
        public void ReturnsNullWhenBufferIsNull() =>
            Assert.Null(SerializationUtility.Deserialize(serializer, null));

        [Fact]
        public void ReturnsNullWhenBufferIsEmpty() =>
            Assert.Null(SerializationUtility.Deserialize(serializer, Array.Empty<byte>()));
    }

    public sealed class Serialize : SerializationUtilityTest
    {
        readonly DataContractSerializer serializer = Serializer;
        readonly object msg = new Payload { Name = fuzzy.String(), Value = fuzzy.Int32() };

        [Fact]
        public void ReturnsBinaryXmlEncodingOfMsg()
        {
            byte[] buffer = SerializationUtility.Serialize(serializer, msg);

            Payload roundTripped = (Payload)SerializationUtility.Deserialize(serializer, buffer);
            Assert.Equal(((Payload)msg).Name, roundTripped.Name);
            Assert.Equal(((Payload)msg).Value, roundTripped.Value);
        }

        [Fact]
        public void ReturnsNullWhenMsgIsNull() =>
            Assert.Null(SerializationUtility.Serialize(serializer, null));
    }

    [DataContract]
    public sealed class Payload
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public int Value { get; set; }
    }
}
