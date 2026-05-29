// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Common;

public abstract class RwLockTest
{
    readonly RwLock sut = new();

    readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

    static readonly TimeSpan BlockedWait = TimeSpan.FromMilliseconds(100);
    static readonly TimeSpan UnblockedWait = TimeSpan.FromSeconds(10);

    public sealed class AcquireReadLock : RwLockTest
    {
        [Fact]
        public void ReturnsDisposable()
        {
            using IDisposable result = sut.AcquireReadLock();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task AllowsConcurrentReadLocks()
        {
            TaskCompletionSource<object> release = await HoldAsync(sut.AcquireReadLock);

            Task second = Task.Run(() => sut.AcquireReadLock().Dispose(), cancellation);

            await AssertCompletes(second);
            release.SetResult(null);
        }

        [Fact]
        public async Task BlocksConcurrentWriteLockUntilDisposed()
        {
            TaskCompletionSource<object> release = await HoldAsync(sut.AcquireReadLock);
            Task write = Task.Run(() => sut.AcquireWriteLock().Dispose(), cancellation);

            await AssertDoesNotComplete(write);
            release.SetResult(null);

            await AssertCompletes(write);
        }

        [Fact]
        public async Task ReturnedDisposableIsIdempotent()
        {
            await Task.Run(() =>
            {
                IDisposable read = sut.AcquireReadLock();
                read.Dispose();

                // Without the !isDisposed guard, the second ExitReadLock would throw SynchronizationLockException.
                read.Dispose();
            }, cancellation);

            // After both Dispose calls the lock must still be released; a write lock would otherwise block forever.
            Task write = Task.Run(() => sut.AcquireWriteLock().Dispose(), cancellation);
            await AssertCompletes(write);
        }
    }

    public sealed class AcquireWriteLock : RwLockTest
    {
        [Fact]
        public void ReturnsDisposable()
        {
            using IDisposable result = sut.AcquireWriteLock();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task BlocksConcurrentReadLockUntilDisposed()
        {
            TaskCompletionSource<object> release = await HoldAsync(sut.AcquireWriteLock);
            Task read = Task.Run(() => sut.AcquireReadLock().Dispose(), cancellation);

            await AssertDoesNotComplete(read);
            release.SetResult(null);

            await AssertCompletes(read);
        }

        [Fact]
        public async Task BlocksConcurrentWriteLockUntilDisposed()
        {
            TaskCompletionSource<object> release = await HoldAsync(sut.AcquireWriteLock);
            Task second = Task.Run(() => sut.AcquireWriteLock().Dispose(), cancellation);

            await AssertDoesNotComplete(second);
            release.SetResult(null);

            await AssertCompletes(second);
        }

        [Fact]
        public async Task ReturnedDisposableIsIdempotent()
        {
            await Task.Run(() =>
            {
                IDisposable write = sut.AcquireWriteLock();
                write.Dispose();

                // Without the !isDisposed guard, the second ExitWriteLock would throw SynchronizationLockException.
                write.Dispose();
            }, cancellation);

            // After both Dispose calls the lock must still be released; a write lock would otherwise block forever.
            Task next = Task.Run(() => sut.AcquireWriteLock().Dispose(), cancellation);
            await AssertCompletes(next);
        }
    }

    // ReaderWriterLockSlim is thread-affine: a lock must be released on the thread that acquired it.
    // HoldAsync acquires the lock on a dedicated worker thread and keeps it held until the returned source is signaled.
    async Task<TaskCompletionSource<object>> HoldAsync(Func<IDisposable> acquire)
    {
        var acquired = new TaskCompletionSource<object>();
        var release = new TaskCompletionSource<object>();
        _ = Task.Run(() =>
        {
            using IDisposable _ = acquire();
            acquired.SetResult(null);
            release.Task.Wait(cancellation);
        }, cancellation);
        await acquired.Task;
        return release;
    }

    async Task AssertCompletes(Task task)
    {
        Task delay = Task.Delay(UnblockedWait, cancellation);
        Assert.Same(task, await Task.WhenAny(task, delay));
        await task;
    }

    async Task AssertDoesNotComplete(Task task)
    {
        Task delay = Task.Delay(BlockedWait, cancellation);
        Assert.Same(delay, await Task.WhenAny(task, delay));
    }
}
