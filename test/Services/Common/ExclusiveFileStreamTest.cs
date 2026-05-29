// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Common;

public abstract class ExclusiveFileStreamTest : IDisposable
{
    readonly ExclusiveFileStream sut;

    // Constructor parameters
    readonly string sutPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ExclusiveFileStreamTest() =>
        sut = ExclusiveFileStream.Acquire(sutPath, FileMode.CreateNew, FileShare.None, FileAccess.ReadWrite);

    void IDisposable.Dispose()
    {
        sut.Dispose();
        File.Delete(sutPath);
        DisposeCore();
    }

    private protected virtual void DisposeCore() { }

    public sealed class Acquire : ExclusiveFileStreamTest
    {
        readonly string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        readonly FileMode fileMode = FileMode.Open;
        readonly FileShare fileShare = FileShare.None;
        readonly FileAccess fileAccess = FileAccess.ReadWrite;

        private protected override void DisposeCore() => File.Delete(path);

        [Fact]
        public void OpensExistingFileWithoutTruncationWhenFileModeIsOpen()
        {
            byte[] expected = fuzzy.Array(fuzzy.Byte);
            File.WriteAllBytes(path, expected);

            using var sut = ExclusiveFileStream.Acquire(path, FileMode.Open, fileShare, fileAccess);

            // FileMode.Open preserves existing content; pins SUT against Create/CreateNew/Truncate.
            var actual = new byte[expected.Length];
            Assert.Equal(expected.Length, sut.Value.Read(actual, 0, actual.Length));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CreatesFileWhenFileModeIsOpenOrCreateAndFileIsMissing()
        {
            Assert.False(File.Exists(path));

            using var sut = ExclusiveFileStream.Acquire(
                path, FileMode.OpenOrCreate, fileShare, fileAccess);

            // FileMode.OpenOrCreate creates a missing file; pins SUT against FileMode.Open.
            Assert.True(File.Exists(path));
            Assert.Equal(path, sut.Value.Name);
        }

        [Theory]
        [InlineData(FileAccess.Read, true, false)]
        [InlineData(FileAccess.Write, false, true)]
        [InlineData(FileAccess.ReadWrite, true, true)]
        public void ForwardsFileAccessToFileOpen(FileAccess access, bool canRead, bool canWrite)
        {
            File.WriteAllBytes(path, fuzzy.Array(fuzzy.Byte));

            using var sut = ExclusiveFileStream.Acquire(path, fileMode, fileShare, access);

            Assert.Equal(canRead, sut.Value.CanRead);
            Assert.Equal(canWrite, sut.Value.CanWrite);
        }

        [Fact]
        public void BlocksConcurrentOpenWhenFileShareIsNone()
        {
            File.WriteAllBytes(path, fuzzy.Array(fuzzy.Byte));

            using var sut = ExclusiveFileStream.Acquire(path, fileMode, FileShare.None, fileAccess);

            // FileShare.None prevents any concurrent open; pins SUT against more permissive shares.
            _ = Assert.Throws<IOException>(() =>
            {
                using FileStream _ = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            });
        }

        [Fact]
        public void AllowsConcurrentReadWhenFileShareIsRead()
        {
            File.WriteAllBytes(path, fuzzy.Array(fuzzy.Byte));

            using var sut = ExclusiveFileStream.Acquire(path, fileMode, FileShare.Read, FileAccess.Read);

            // FileShare.Read allows concurrent readers; pins SUT against FileShare.None.
            using FileStream concurrent = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Assert.Equal(path, concurrent.Name);

            // Concurrent write must fail; pins SUT against the more permissive FileShare.ReadWrite.
            _ = Assert.Throws<IOException>(() =>
            {
                using FileStream _ = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            });
        }

        [Fact(Explicit = true)] // TODO: Flaky test.
        public async Task RetriesUntilFileBecomesAvailable()
        {
            // Races Task.Delay(250) against thread-pool scheduling and the SUT's non-injectable
            // Thread.Sleep(Rand.Next(100, 1000)); the delegate may not reach File.Open before
            // locked.Dispose() (silent false success) or the SUT's random sleep plus a subsequent
            // File.Open may exceed cancellation. Deterministic injection of the clock/sleeper is out of scope.
            var fileAccess = FileAccess.Read;
            CancellationToken cancellation = TestContext.Current.CancellationToken;
            using FileStream locked = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);

            Task<ExclusiveFileStream> acquire = Task.Run(
                () => ExclusiveFileStream.Acquire(path, fileMode, fileShare, fileAccess),
                cancellation);

            while (acquire.Status < TaskStatus.Running)
                await Task.Yield();
            await Task.Delay(250, cancellation);
            locked.Dispose();

            using ExclusiveFileStream sut = await acquire;
            Assert.Equal(path, sut.Value.Name);
        }

        [Fact(Explicit = true)] // TODO: SUT testability limitation. MaxAttempts/Thread.Sleep/Random not injectable.
        public void ThrowsIOExceptionWhenFileRemainsLockedAfterMaxAttempts()
        {
            // ExclusiveFileStream hard-codes MaxAttempts=60 and Thread.Sleep with a non-injectable
            // Random in 100..1000 ms. Deterministically exercising the rethrow branch requires
            // injecting the sleeper and clock into the SUT, which is out of scope here.
            throw new NotImplementedException();
        }
    }

    public sealed class Dispose : ExclusiveFileStreamTest
    {
        [Fact]
        public void DisposesUnderlyingFileStream()
        {
            FileStream stream = sut.Value;

            sut.Dispose();

            Assert.False(stream.CanRead);
            Assert.False(stream.CanWrite);
        }
    }
}
