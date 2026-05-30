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
        public async Task AllowsConcurrentReadLocks()
        {
            await using Holder holder = await Hold(sut.AcquireReadLock);

            Task second = Task.Run(() => sut.AcquireReadLock().Dispose(), cancellation);

            await AssertCompletes(second);
        }

        [Fact]
        public async Task BlocksConcurrentWriteLockUntilDisposed()
        {
            await using Holder holder = await Hold(sut.AcquireReadLock);
            Task write = await StartAcquire(sut.AcquireWriteLock);

            await AssertDoesNotComplete(write);
            await holder.DisposeAsync();

            await AssertCompletes(write);
        }

        [Fact]
        public async Task ReturnsIdempotentDisposable()
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
        public async Task BlocksConcurrentReadLockUntilDisposed()
        {
            await using Holder holder = await Hold(sut.AcquireWriteLock);
            Task read = await StartAcquire(sut.AcquireReadLock);

            await AssertDoesNotComplete(read);
            await holder.DisposeAsync();

            await AssertCompletes(read);
        }

        [Fact]
        public async Task BlocksConcurrentWriteLockUntilDisposed()
        {
            await using Holder holder = await Hold(sut.AcquireWriteLock);
            Task second = await StartAcquire(sut.AcquireWriteLock);

            await AssertDoesNotComplete(second);
            await holder.DisposeAsync();

            await AssertCompletes(second);
        }

        [Fact]
        public async Task ReturnsIdempotentDisposable()
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
    // Hold acquires the lock on a dedicated worker thread and keeps it held until the returned holder is disposed.
    // Disposal is asynchronous so the worker can be awaited, surfacing any failures from acquire or release on the
    // caller's thread and preventing a faulting worker from being stranded if an assertion fails before release.
    async Task<Holder> Hold(Func<IDisposable> acquire)
    {
        var acquired = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task worker = Task.Run(() =>
        {
            IDisposable held;
            try
            {
                held = acquire();
            }
            catch (Exception ex)
            {
                acquired.TrySetException(ex);
                throw;
            }
            acquired.SetResult(null);
            try
            {
                release.Task.Wait(cancellation);
            }
            finally
            {
                held.Dispose();
            }
        });
        await acquired.Task;
        return new Holder(release, worker);
    }

    sealed class Holder(TaskCompletionSource<object> release, Task worker) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            release.TrySetResult(null);
            await worker;
        }
    }

    // Signals readiness from inside the worker immediately before the acquire call so the caller can wait until the
    // competing task is about to attempt the acquire. Without this, AssertDoesNotComplete could observe the BlockedWait
    // elapse before the thread pool ever scheduled the worker, yielding a false positive.
    async Task<Task> StartAcquire(Func<IDisposable> acquire)
    {
        var ready = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task task = Task.Run(() =>
        {
            ready.SetResult(null);
            acquire().Dispose();
        });
        await ready.Task;
        return task;
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
        Task finished = await Task.WhenAny(task, delay);
        if (finished == task)
            await task;
        Assert.Same(delay, finished);
    }
}
