// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data.Collections.Beta
{
    using System;
    using System.Fabric;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides extension methods for <see cref="IReliableDictionary4{TKey, TValue}"/>.
    /// </summary>
    public static class IReliableDictionaryExtensions
    {
        /// <summary>
        /// Asynchronously attempts to remove the value with the specified key without reading data from the disk
        /// and returns <see langword="true"/> if the key was removed from the Reliable Dictionary; otherwise, <see langword="false"/>.
        /// </summary>
        /// <remarks>
        /// This overload removes the key with a fixed four-second timeout and cannot be canceled. Call the
        /// <see cref="IReliableDictionary4{TKey, TValue}"/> overload that accepts a <see cref="TimeSpan"/> and a
        /// <see cref="CancellationToken"/> to control the timeout or cancel the operation.
        /// </remarks>
        /// <typeparam name="TKey">The type of the keys in the reliable dictionary.</typeparam>
        /// <typeparam name="TValue">
        /// The type of the values in the reliable dictionary.</typeparam>
        /// <param name="reliableDictionary4Interface">The reliable dictionary to remove the element from.</param>
        /// <param name="tx">The transaction to associate this operation with.</param>
        /// <param name="key">The key of the element to remove.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>, or <paramref name="key"/> is <see langword="null"/>.</exception>
        /// <exception cref="TimeoutException">The operation failed to complete within the four-second timeout.</exception>
        /// <exception cref="FabricNotPrimaryException">The <see cref="IReliableDictionary4{TKey, TValue}"/> is not in <see cref="ReplicaRole.Primary"/>.</exception>
        /// <exception cref="TransactionFaultedException">The transaction has been internally faulted by the system. Retry the operation on a new transaction.</exception>
        /// <exception cref="InvalidOperationException">A method call is invalid for the object's current state, for example, the transaction is already committed or aborted.</exception>
        /// <exception cref="FabricObjectClosedException">The <see cref="IReliableDictionary4{TKey, TValue}"/> is closed or deleted.</exception>
        public static Task<bool> RemoveAsync<TKey, TValue>(this IReliableDictionary4<TKey, TValue> reliableDictionary4Interface, ITransaction tx, TKey key)
            where TKey : IComparable<TKey>, IEquatable<TKey>
        {
            return reliableDictionary4Interface.RemoveAsync(tx, key, TimeSpan.FromSeconds(4), CancellationToken.None);
        }
    }
}
