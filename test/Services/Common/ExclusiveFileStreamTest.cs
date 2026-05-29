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

    readonly string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    void IDisposable.Dispose() => DeleteTempFile();

    protected void DeleteTempFile()
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public sealed class Acquire : ExclusiveFileStreamTest
    {
        [Fact]
        public void OpensFileAtGivenPathWithGivenModeShareAndAccess()
        {
            byte[] expected = fuzzy.Array(fuzzy.Byte);
            File.WriteAllBytes(path, expected);

            using ExclusiveFileStream sut = ExclusiveFileStream.Acquire(path, FileMode.Open, FileShare.None, FileAccess.ReadWrite);

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

        [Fact]
        public async Task RetriesUntilFileBecomesAvailable()
        {
            CancellationToken cancellation = TestContext.Current.CancellationToken;
            using FileStream locked = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);

            Task<ExclusiveFileStream> acquireTask = Task.Run(
                () => ExclusiveFileStream.Acquire(path, FileMode.Open, FileShare.None, FileAccess.Read),
                cancellation);

            // Wait until acquireTask is scheduled so thread-pool jitter cannot release the lock before
            // the SUT's first open attempt. Then hold long enough that the first attempt is guaranteed
            // to observe the lock and enter the retry sleep (Thread.Sleep up to 1000 ms in the SUT).
            while (acquireTask.Status < TaskStatus.Running)
                await Task.Yield();
            await Task.Delay(250, cancellation);
            locked.Dispose();

            // Total runtime is still nondeterministic because the SUT's retry interval is a random
            // 100..1000 ms drawn from a non-injectable Random; making it deterministic requires
            // injecting the sleeper and clock into ExclusiveFileStream, which is out of scope here.
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

    public sealed class Dispose : ExclusiveFileStreamTest, IDisposable
    {
        readonly ExclusiveFileStream sut;

        public Dispose() =>
            sut = ExclusiveFileStream.Acquire(path, FileMode.CreateNew, FileShare.None, FileAccess.ReadWrite);

        void IDisposable.Dispose()
        {
            sut.Dispose();
            DeleteTempFile();
        }

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
