// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    /// <summary>
    /// Exposes an <see cref="IAsyncEnumerator{T}"/> 
    /// which supports an asynchronous iteration over a collection 
    /// of a specified type.
    /// </summary>
    public interface IAsyncEnumerable<out T>
    {
        /// <summary>
        /// Returns an <see cref="IAsyncEnumerator{T}"/> 
        /// that asynchronously iterates through the collection.
        /// </summary>
        IAsyncEnumerator<T> GetAsyncEnumerator();
    }
}
