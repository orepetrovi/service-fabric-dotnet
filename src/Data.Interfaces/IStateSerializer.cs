// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    using System.IO;

    /// <summary>
    /// Represents a custom serializer for type <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Use <see cref="IReliableStateManager.TryAddStateSerializer{T}(IStateSerializer{T})"/> to register a custom serializer.
    /// </remarks>
    /// <example>
    /// In this example, implementation of the <c>Read</c> and <c>Write</c> overloads simply call their counterpart overloads.
    /// The <c>baseValue</c> parameter on the second <c>Read</c> overload and the <c>baseValue</c> parameter on the second <c>Write</c> overload are not set by the platform and should be ignored.
    /// <code language="csharp">
    /// class Order
    /// {
    ///     public byte Warehouse { get; set; }
    ///     public short District { get; set; }
    ///     public int Customer { get; set; }
    ///     public long OrderNumber { get; set; }
    /// }
    ///
    /// class OrderSerializer : IStateSerializer&lt;Order&gt;
    /// {
    ///     public void Write(Order value, BinaryWriter binaryWriter)
    ///     {
    ///         binaryWriter.Write(value.Warehouse);
    ///         binaryWriter.Write(value.District);
    ///         binaryWriter.Write(value.Customer);
    ///         binaryWriter.Write(value.OrderNumber);
    ///     }
    ///
    ///     public Order Read(BinaryReader binaryReader)
    ///     {
    ///         Order value = new Order();
    ///
    ///         value.Warehouse = binaryReader.ReadByte();
    ///         value.District = binaryReader.ReadInt16();
    ///         value.Customer = binaryReader.ReadInt32();
    ///         value.OrderNumber = binaryReader.ReadInt64();
    ///
    ///         return value;
    ///     }
    ///
    ///     public void Write(Order baseValue, Order targetValue, BinaryWriter binaryWriter)
    ///     {
    ///         this.Write(targetValue, binaryWriter);
    ///     }
    ///
    ///     public Order Read(Order baseValue, BinaryReader binaryReader)
    ///     {
    ///         return this.Read(binaryReader);
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IStateSerializer<T>
    {
        /// <summary>
        /// Returns a value of type <typeparamref name="T"/> deserialized from the given <see cref="BinaryReader"/>.
        /// </summary>
        /// <remarks>
        /// When accessing the <see cref="BinaryReader"/> base stream, care must be taken when moving the position in the stream.
        /// Reading must begin at the current stream position and end at the current position plus the length of your data.
        /// </remarks>
        T Read(BinaryReader binaryReader);

        /// <summary>
        /// Serializes a value and writes it to the given <see cref="BinaryWriter"/>.
        /// </summary>
        /// <remarks>
        /// When accessing the <see cref="BinaryWriter"/> base stream, care must be taken when moving the position in the stream.
        /// Writing must begin at the current stream position and end at the current position plus the length of your data.
        /// </remarks>
        void Write(T value, BinaryWriter binaryWriter);

        /// <summary>
        /// Returns a value of type <typeparamref name="T"/> deserialized from the given <see cref="BinaryReader"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When accessing the <see cref="BinaryReader"/> base stream, care must be taken when moving the position in the stream.
        /// Reading must begin at the current stream position and end at the current position plus the length of your data.
        /// </para>
        /// <para>
        /// The platform currently does not populate <paramref name="baseValue"/>; implementers can ignore it.
        /// See the example on <see cref="IStateSerializer{T}"/>.
        /// </para>
        /// </remarks>
        T Read(T baseValue, BinaryReader binaryReader);

        /// <summary>
        /// Serializes a value and writes it to the given <see cref="BinaryWriter"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When accessing the <see cref="BinaryWriter"/> base stream, care must be taken when moving the position in the stream.
        /// Writing must begin at the current stream position and end at the current position plus the length of your data.
        /// </para>
        /// <para>
        /// The platform currently does not populate <paramref name="baseValue"/>; implementers can ignore it.
        /// See the example on <see cref="IStateSerializer{T}"/>.
        /// </para>
        /// </remarks>
        void Write(T baseValue, T targetValue, BinaryWriter binaryWriter);
    }
}
