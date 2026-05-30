// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Common;

public abstract class SerializationUtilityTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    static readonly DataContractSerializer payloadSerializer = new(typeof(Payload));

    public sealed class Deserialize : SerializationUtilityTest
    {
        // Method parameters
        readonly DataContractSerializer serializer = payloadSerializer;
        readonly byte[] buffer;

        readonly Payload msg = new() { Name = fuzzy.String(), Value = fuzzy.Int32() };

        public Deserialize() =>
            buffer = Encode(msg);

        [Fact]
        public void ReturnsMsgFromBinaryXmlEncoding()
        {
            var result = (Payload)SerializationUtility.Deserialize(serializer, buffer);

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
        readonly DataContractSerializer serializer = payloadSerializer;
        readonly Payload msg = new() { Name = fuzzy.String(), Value = fuzzy.Int32() };

        [Fact]
        public void ReturnsBinaryXmlEncodingOfMsg()
        {
            byte[] buffer = SerializationUtility.Serialize(serializer, msg);

            Assert.Equal(Encode(msg), buffer);
        }

        [Fact]
        public void ReturnsNullWhenMsgIsNull() =>
            Assert.Null(SerializationUtility.Serialize(serializer, null));
    }

    static byte[] Encode(object msg)
    {
        using var stream = new MemoryStream();
        using var writer = XmlDictionaryWriter.CreateBinaryWriter(stream);
        payloadSerializer.WriteObject(writer, msg);
        writer.Flush();
        return stream.ToArray();
    }

    [DataContract]
    public sealed class Payload
    {
        [DataMember]
        public string Name;

        [DataMember]
        public int Value;
    }
}
