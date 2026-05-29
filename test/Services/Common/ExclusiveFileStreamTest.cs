// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Common;

public abstract class ExclusiveFileStreamTest : IDisposable
{
    protected readonly string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    void IDisposable.Dispose()
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public sealed class Acquire : ExclusiveFileStreamTest
    {
        // Method parameters
        readonly FileMode fileMode = FileMode.CreateNew;
        readonly FileShare fileShare = FileShare.None;
        readonly FileAccess fileAccess = FileAccess.ReadWrite;

        [Fact]
        public void OpensFileAtGivenPathWithGivenModeShareAndAccess()
        {
            using var sut = ExclusiveFileStream.Acquire(path, fileMode, fileShare, fileAccess);

            Assert.Equal(path, sut.Value.Name);
            Assert.True(sut.Value.CanRead);
            Assert.True(sut.Value.CanWrite);
            Assert.True(File.Exists(path));
        }

        [Fact]
        public async Task RetriesUntilFileBecomesAvailable()
        {
            var cancellation = TestContext.Current.CancellationToken;
            var locked = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);

            var acquireTask = Task.Run(
                () => ExclusiveFileStream.Acquire(path, FileMode.Open, FileShare.None, FileAccess.Read),
                cancellation);

            // Hold the lock long enough to force at least one retry, then release.
            await Task.Delay(250, cancellation);
            locked.Dispose();

            using var sut = await acquireTask;
            Assert.Equal(path, sut.Value.Name);
        }

        [Fact(Explicit = true)] // TODO: Slow. Exhausts 60+ retries each sleeping 100-1000ms.
        public void ThrowsIOExceptionWhenFileRemainsLockedAfterMaxAttempts()
        {
            using var locked = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);

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
            var stream = sut.Value;

            sut.Dispose();

            Assert.False(stream.CanRead);
            Assert.False(stream.CanWrite);
        }
    }
}
