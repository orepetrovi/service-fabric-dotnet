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
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    readonly ExclusiveFileStream sut;
    readonly string sutPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    readonly string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    ExclusiveFileStreamTest() =>
        sut = ExclusiveFileStream.Acquire(sutPath, FileMode.CreateNew, FileShare.None, FileAccess.ReadWrite);

    void IDisposable.Dispose()
    {
        sut.Dispose();
        if (File.Exists(sutPath))
            File.Delete(sutPath);
        DeleteTempFile();
    }

    void DeleteTempFile()
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public sealed class Acquire : ExclusiveFileStreamTest
    {
        readonly FileMode fileMode = FileMode.Open;
        readonly FileShare fileShare = FileShare.None;
        readonly FileAccess fileAccess = FileAccess.ReadWrite;

        [Fact]
        public void OpensFileAtGivenPathWithGivenModeShareAndAccess()
        {
            byte[] expected = fuzzy.Array(fuzzy.Byte);
            File.WriteAllBytes(path, expected);

            using ExclusiveFileStream sut = ExclusiveFileStream.Acquire(path, fileMode, fileShare, fileAccess);

            Assert.Equal(path, sut.Value.Name);
            Assert.True(sut.Value.CanRead);
            Assert.True(sut.Value.CanWrite);

            // FileMode.Open preserves existing content; pins SUT against Create/CreateNew/Truncate.
            byte[] actual = new byte[expected.Length];
            Assert.Equal(expected.Length, sut.Value.Read(actual, 0, actual.Length));
            Assert.Equal(expected, actual);

            // FileShare.None prevents any concurrent open; pins SUT against more permissive shares.
            _ = Assert.Throws<IOException>(() =>
            {
                using FileStream _ = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            });
        }

        // TODO: Flaky test.
        [Fact(Explicit = true)]
        public async Task RetriesUntilFileBecomesAvailable()
        {
            // Races Task.Delay(250) against thread-pool scheduling and the SUT's non-injectable
            // Thread.Sleep(Rand.Next(100, 1000)); the delegate may not reach File.Open before
            // locked.Dispose() (silent false success) or the SUT's random sleep plus a subsequent
            // File.Open may exceed cancellation. Deterministic injection of the clock/sleeper is out of scope.
            FileAccess fileAccess = FileAccess.Read;
            CancellationToken cancellation = TestContext.Current.CancellationToken;
            using FileStream locked = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);

            Task<ExclusiveFileStream> acquireTask = Task.Run(
                () => ExclusiveFileStream.Acquire(path, fileMode, fileShare, fileAccess),
                cancellation);

            while (acquireTask.Status < TaskStatus.Running)
                await Task.Yield();
            await Task.Delay(250, cancellation);
            locked.Dispose();

            using ExclusiveFileStream sut = await acquireTask;
            Assert.Equal(path, sut.Value.Name);
        }

        // TODO: SUT testability limitation. MaxAttempts/Thread.Sleep/Random not injectable.
        [Fact(Explicit = true)]
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
