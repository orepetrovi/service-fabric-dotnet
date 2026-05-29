// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Linq;
using System.Text;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Services;

public abstract class CRC64Test
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    // CRC-64/WE check value for the ASCII input "123456789".
    // See https://reveng.sourceforge.io/crc-catalogue/all.htm#crc.cat.crc-64-we.
    const ulong Crc64WeCheck = 0x62EC59E3F1A4F00A;

    static readonly byte[] CheckInput = Encoding.ASCII.GetBytes("123456789");

    public sealed class ToCRC64_ByteArray : CRC64Test
    {
        readonly byte[] value = fuzzy.Array(fuzzy.Byte);

        [Fact]
        public void ReturnsExpectedCrc64ForCheckInput() =>
            Assert.Equal(Crc64WeCheck, CRC64.ToCRC64(CheckInput));

        [Fact]
        public void ReturnsZeroWhenValueIsEmpty() =>
            Assert.Equal(0UL, CRC64.ToCRC64(Array.Empty<byte>()));

        [Fact]
        public void ReturnsSameValueForEqualInputs() =>
            Assert.Equal(CRC64.ToCRC64(value), CRC64.ToCRC64((byte[])value.Clone()));

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenValueIsNull() =>
            Assert.Equal(nameof(value), Assert.Throws<ArgumentNullException>(() => CRC64.ToCRC64((byte[])null)).ParamName);
    }

    public sealed class ToCRC64_ByteArrayArray : CRC64Test
    {
        readonly byte[][] values = fuzzy.Array(() => fuzzy.Array(fuzzy.Byte));

        [Fact]
        public void ReturnsExpectedCrc64ForCheckInputSplitAcrossArrays() =>
            Assert.Equal(Crc64WeCheck, CRC64.ToCRC64(CheckInput.Take(4).ToArray(), CheckInput.Skip(4).ToArray()));

        [Fact]
        public void ReturnsSameValueAsSingleArrayOverloadForConcatenatedInput() =>
            Assert.Equal(CRC64.ToCRC64(values.SelectMany(v => v).ToArray()), CRC64.ToCRC64(values));

        [Fact]
        public void ReturnsZeroWhenValuesAreEmpty() =>
            Assert.Equal(0UL, CRC64.ToCRC64(Array.Empty<byte[]>()));

        [Fact]
        public void ReturnsZeroWhenAllArraysAreEmpty() =>
            Assert.Equal(0UL, CRC64.ToCRC64(Array.Empty<byte>(), Array.Empty<byte>()));

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenValuesIsNull() =>
            Assert.Equal(nameof(values), Assert.Throws<ArgumentNullException>(() => CRC64.ToCRC64((byte[][])null)).ParamName);

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenAnyArrayIsNull() =>
            Assert.Equal(nameof(values), Assert.Throws<ArgumentNullException>(() => CRC64.ToCRC64(new byte[][] { null })).ParamName);
    }

    public sealed class ToCrc64String : CRC64Test
    {
        readonly byte[] value = fuzzy.Array(fuzzy.Byte);

        [Fact]
        public void ReturnsUppercaseHexadecimalRepresentationOfCrc64() =>
            Assert.Equal("62EC59E3F1A4F00A", CRC64.ToCrc64String(CheckInput));

        [Fact]
        public void ReturnsZeroWhenValueIsEmpty() =>
            Assert.Equal("0", CRC64.ToCrc64String(Array.Empty<byte>()));

        [Fact]
        public void ReturnsSameStringForEqualInputs() =>
            Assert.Equal(CRC64.ToCrc64String(value), CRC64.ToCrc64String((byte[])value.Clone()));

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenValueIsNull() =>
            Assert.Equal(nameof(value), Assert.Throws<ArgumentNullException>(() => CRC64.ToCrc64String(null)).ParamName);
    }
}
