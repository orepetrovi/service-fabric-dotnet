// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Common;

public abstract class ExclusiveFileStreamTest : IDisposable
{
    readonly string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    void IDisposable.Dispose()
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public sealed class Acquire : ExclusiveFileStreamTest
    {
        [Fact]
        public void OpensFileAtGivenPathWithGivenModeShareAndAccess()
        {
            byte[] expected = Guid.NewGuid().ToByteArray();
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
            _ = Assert.Throws<IOException>(() => File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        [Fact]
        public async Task RetriesUntilFileBecomesAvailable()
        {
            CancellationToken cancellation = TestContext.Current.CancellationToken;
            FileStream locked = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);

            Task<ExclusiveFileStream> acquireTask = Task.Run(
                () => ExclusiveFileStream.Acquire(path, FileMode.Open, FileShare.None, FileAccess.Read),
                cancellation);

            // Hold the lock long enough to force at least one retry, then release.
            await Task.Delay(250, cancellation);
            locked.Dispose();

            using ExclusiveFileStream sut = await acquireTask;
            Assert.Equal(path, sut.Value.Name);
        }

        // SUT testability gap: ExclusiveFileStream hard-codes MaxAttempts, the retry interval bounds, the
        // sleeper (Thread.Sleep), and the clock (Random). Exercising the max-attempt rethrow branch
        // deterministically requires injecting those collaborators, which is out of scope for this work.
        // Until the SUT is made testable, this test is marked Explicit so it does not add 30-60s to every
        // run; covering the rethrow branch by default would require either accepting that slowdown or
        // changing the SUT.
        [Fact(Explicit = true)]
        public void ThrowsIOExceptionWhenFileRemainsLockedAfterMaxAttempts()
        {
            using FileStream locked = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);

            _ = Assert.Throws<IOException>(() =>
                ExclusiveFileStream.Acquire(path, FileMode.Open, FileShare.None, FileAccess.Read));
        }
    }

    public sealed class Dispose : ExclusiveFileStreamTest
    {
        readonly ExclusiveFileStream sut;

        public Dispose() =>
            sut = ExclusiveFileStream.Acquire(path, FileMode.CreateNew, FileShare.None, FileAccess.ReadWrite);

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
