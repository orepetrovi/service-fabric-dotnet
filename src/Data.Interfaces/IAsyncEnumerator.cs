// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Supports asynchronous iteration over a sequence of values of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of values produced by the enumerator.</typeparam>
    /// <remarks>
    /// Obtained from <see cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>.
    /// </remarks>
    public interface IAsyncEnumerator<out T> : IDisposable
    {
        /// <summary>
        /// Gets the element at the current position of the enumerator.
        /// </summary>
        /// <remarks>
        /// The value is undefined before the first call to <see cref="MoveNextAsync(CancellationToken)"/>
        /// and after a call returns <see langword="false"/>.
        /// </remarks>
        T Current { get; }

        /// <summary>
        /// Returns a value that indicates whether the enumerator was successfully advanced to the next element of the sequence.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator was advanced to the next element; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">The underlying collection was modified after the enumerator was created.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
        Task<bool> MoveNextAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Sets the enumerator to its initial position, before the first element of the sequence.
        /// </summary>
        /// <exception cref="InvalidOperationException">The underlying collection was modified after the enumerator was created.</exception>
        void Reset();
    }
}
