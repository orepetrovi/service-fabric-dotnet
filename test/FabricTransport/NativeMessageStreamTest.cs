// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Generic;
using System.Fabric.Interop;
using System.IO;
using System.Linq;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.FabricTransport;

public abstract class NativeMessageStreamTest: IDisposable
{
    readonly Stream sut;

    // Constructor parameters
    readonly List<Tuple<uint, nint>> bufferList;

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);
    readonly PinCollection pins = [];
    readonly List<byte[]> managedBuffers = fuzzy.List(
        () => fuzzy.Array(fuzzy.Byte, Fuzzy.Length.Min(1)),
        Fuzzy.Count.Min(2));
    readonly byte[] expectedBytes;

    NativeMessageStreamTest()
    {
        bufferList = [.. managedBuffers.Select(_ => NativeTypes.ToNativeBytes(pins, _))];
        expectedBytes = [.. managedBuffers.SelectMany(_ => _)];
        sut = new NativeMessageStream(bufferList);
    }

    void IDisposable.Dispose() =>
        pins.Dispose();

    public sealed class Constructor: NativeMessageStreamTest
    {
        [Fact]
        public void InitializesLengthToSumOfBufferLengths() =>
            Assert.Equal(managedBuffers.Sum(_ => _.Length), sut.Length);

        [Fact]
        public void InitializesLengthToZeroWhenMessageIsEmpty()
        {
            using var emptySut = new NativeMessageStream([]);
            Assert.Equal(0, emptySut.Length);
        }

        [Fact]
        public void InitializesPositionToZero() =>
            Assert.Equal(0, sut.Position);

        [Fact(Explicit = true)] // TODO: SUT bug. Initialize does not validate bufferList; null surfaces as NullReferenceException from SetLength.
        public void ThrowsArgumentNullExceptionWhenBufferListIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new NativeMessageStream(null));
            Assert.Equal(nameof(bufferList), exception.ParamName);
        }
    }

    public sealed class CanRead: NativeMessageStreamTest
    {
        [Fact]
        public void ReturnsTrue() =>
            Assert.True(sut.CanRead);
    }

    public sealed class CanSeek: NativeMessageStreamTest
    {
        [Fact]
        public void ReturnsTrue() =>
            Assert.True(sut.CanSeek);
    }

    public sealed class CanWrite: NativeMessageStreamTest
    {
        [Fact]
        public void ReturnsFalse() =>
            Assert.False(sut.CanWrite);
    }

    public sealed class Dispose: NativeMessageStreamTest
    {
        [Fact]
        public void ClearsBufferList()
        {
            sut.Dispose();
            Assert.Empty(bufferList);
        }

        [Fact]
        public void DoesNotThrowWhenCalledMultipleTimes()
        {
            sut.Dispose();
            sut.Dispose();
        }
    }

    public sealed class Flush: NativeMessageStreamTest
    {
        [Fact]
        public void DoesNotChangePosition()
        {
            _ = sut.ReadByte();
            long expected = sut.Position;
            sut.Flush();
            Assert.Equal(expected, sut.Position);
        }
    }

    public sealed class Position: NativeMessageStreamTest
    {
        [Fact]
        public void IsSetToGivenValue()
        {
            int expected = fuzzy.Int32();
            sut.Position = expected;
            Assert.Equal(expected, sut.Position);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Position setter casts value to int, truncating values outside the int range.
        public void IsSetToGivenValueWhenValueExceedsIntMaxValue()
        {
            long expected = fuzzy.Int64().Minimum((long)int.MaxValue + 1);
            sut.Position = expected;
            Assert.Equal(expected, sut.Position);
        }
    }

    public sealed class Read: NativeMessageStreamTest
    {
        // Method parameters
        readonly byte[] buffer;
        readonly int offset = 0;
        readonly int count;

        public Read()
        {
            count = expectedBytes.Length;
            buffer = new byte[count];
        }

        [Fact]
        public void CopiesEntireStreamAcrossNativeBuffers()
        {
            int bytesRead = sut.Read(buffer, offset, count);

            Assert.Equal(expectedBytes.Length, bytesRead);
            Assert.Equal(expectedBytes, buffer);
        }

        [Fact]
        public void AdvancesPositionByCumulativeNumberOfBytesRead()
        {
            int first = sut.Read(buffer, 0, 1);
            int second = sut.Read(buffer, 0, 1);
            Assert.Equal(first + second, sut.Position);
        }

        [Fact]
        public void ReadsSequentiallyAcrossMultipleCalls()
        {
            int firstChunk = expectedBytes.Length / 2;

            int firstRead = sut.Read(buffer, offset, firstChunk);
            int secondRead = sut.Read(buffer, firstRead, count - firstRead);

            Assert.Equal(firstChunk, firstRead);
            Assert.Equal(expectedBytes.Length - firstChunk, secondRead);
            Assert.Equal(expectedBytes, buffer);
        }

        [Fact]
        public void WritesAtGivenOffsetInOutputBuffer()
        {
            int prefix = fuzzy.Int32().Between(1, 5);
            byte[] output = fuzzy.Array(fuzzy.Byte, Fuzzy.Length.Exactly(count + prefix));
            byte[] originalPrefix = [.. output.Take(prefix)];

            int bytesRead = sut.Read(output, prefix, count);

            Assert.Equal(count, bytesRead);
            Assert.Equal(originalPrefix, output.Take(prefix));
            Assert.Equal(expectedBytes, output.Skip(prefix));
        }

        [Fact]
        public void ReturnsRemainingLengthWhenCountExceedsRemaining()
        {
            byte[] larger = fuzzy.Array(fuzzy.Byte, Fuzzy.Length.Exactly(count + fuzzy.Int32().Between(10, 20)));
            byte[] originalSuffix = [.. larger.Skip(count)];

            int bytesRead = sut.Read(larger, offset, larger.Length);

            Assert.Equal(count, bytesRead);
            Assert.Equal(expectedBytes, larger.Take(bytesRead));
            Assert.Equal(originalSuffix, larger.Skip(bytesRead));
        }

        [Fact]
        public void ReturnsZeroWhenThereAreNoMoreBytesToRead()
        {
            _ = sut.Read(buffer, offset, count);
            int bytesRead = sut.Read(buffer, offset, count);
            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void ReturnsZeroWhenMessageIsEmpty()
        {
            using var emptySut = new NativeMessageStream([]);
            Assert.Equal(0, emptySut.Read(buffer, offset, count));
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Read throws ArgumentNullException without ParamName.
        public void ThrowsArgumentNullExceptionWhenBufferIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => sut.Read(null, offset, count));
            Assert.Equal(nameof(buffer), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Read throws ArgumentOutOfRangeException without ParamName.
        public void ThrowsArgumentOutOfRangeExceptionWhenOffsetIsNegative()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => sut.Read(buffer, -1, count));
            Assert.Equal(nameof(offset), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Read throws ArgumentOutOfRangeException without ParamName.
        public void ThrowsArgumentOutOfRangeExceptionWhenCountIsNegative()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => sut.Read(buffer, offset, -1));
            Assert.Equal(nameof(count), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Read throws ArgumentOutOfRangeException without ParamName.
        public void ThrowsArgumentOutOfRangeExceptionWhenOffsetPlusCountExceedsBufferLength()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => sut.Read(buffer, 1, count));
            Assert.Equal(nameof(count), exception.ParamName);
        }
    }

    public sealed class ReadByte: NativeMessageStreamTest
    {
        [Fact]
        public void ReturnsBytesSequentiallyAcrossNativeBuffers()
        {
            for (int i = 0; i < expectedBytes.Length; i++)
                Assert.Equal((int)expectedBytes[i], sut.ReadByte());
        }

        [Fact]
        public void AdvancesPositionByOne()
        {
            _ = sut.ReadByte();
            long before = sut.Position;
            _ = sut.ReadByte();
            Assert.Equal(before + 1, sut.Position);
        }

        [Fact]
        public void ReturnsMinusOneAtEndOfStream()
        {
            for (int i = 0; i < expectedBytes.Length; i++)
                _ = sut.ReadByte();

            Assert.Equal(-1, sut.ReadByte());
        }
    }

    public sealed class Seek: NativeMessageStreamTest
    {
        // Method parameters
        readonly long offset;
        readonly SeekOrigin origin = SeekOrigin.Begin;

        public Seek() =>
            offset = fuzzy.Int64().Between(1, expectedBytes.Length - 1);

        [Fact]
        public void ResetsPositionToZeroWhenOriginIsBegin()
        {
            _ = sut.Read(new byte[expectedBytes.Length], 0, expectedBytes.Length);

            long result = sut.Seek(0, origin);

            Assert.Equal(0, result);
            Assert.Equal(0, sut.Position);
        }

        [Fact]
        public void RestartsReadingFromBeginningAfterSeekToBegin()
        {
            _ = sut.Read(new byte[expectedBytes.Length], 0, expectedBytes.Length);

            _ = sut.Seek(0, origin);

            var actual = new byte[expectedBytes.Length];
            int bytesRead = sut.Read(actual, 0, actual.Length);
            Assert.Equal(expectedBytes.Length, bytesRead);
            Assert.Equal(expectedBytes, actual);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Seek ignores offset and always resets to 0.
        public void SetsPositionToOffsetWhenOriginIsBegin()
        {
            long result = sut.Seek(offset, origin);

            Assert.Equal(offset, result);
            Assert.Equal(offset, sut.Position);
            Assert.Equal(expectedBytes[(int)offset], sut.ReadByte());
        }

        [Fact]
        public void ThrowsNotImplementedExceptionWhenOriginIsCurrent() =>
            _ = Assert.Throws<NotImplementedException>(() => sut.Seek(offset, SeekOrigin.Current));

        [Fact]
        public void ThrowsNotImplementedExceptionWhenOriginIsEnd() =>
            _ = Assert.Throws<NotImplementedException>(() => sut.Seek(offset, SeekOrigin.End));
    }

    public sealed class SetLength: NativeMessageStreamTest
    {
        // Method parameters
        readonly long value = fuzzy.Int64();

        [Fact]
        public void ThrowsNotImplementedException() =>
            _ = Assert.Throws<NotImplementedException>(() => sut.SetLength(value));
    }

    public sealed class Write: NativeMessageStreamTest
    {
        // Method parameters
        readonly byte[] buffer = fuzzy.Array(fuzzy.Byte);
        readonly int offset = 0;
        readonly int count;

        public Write() => count = buffer.Length;

        [Fact]
        public void ThrowsNotImplementedException() =>
            _ = Assert.Throws<NotImplementedException>(() => sut.Write(buffer, offset, count));
    }
}
