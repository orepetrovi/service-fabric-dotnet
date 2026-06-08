// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data.Collections
{
    using System;
    using System.Fabric;

    // todo: when queue capacity constraint is configurable, remove "not implemented" para
    /// <summary>
    /// Thrown by <see cref="IReliableConcurrentQueue{T}.EnqueueAsync"/> when the queue capacity has been reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Retriable; when encountering this exception, the caller should wait some time for additional dequeue operations
    /// before retrying the enqueue.
    /// </para>
    /// <para>
    /// Queue capacity is not currently implemented.
    /// </para>
    /// </remarks>
    public class QueueFullException : FabricTransientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueueFullException"/> class.
        /// </summary>
        public QueueFullException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueFullException"/> class.
        /// </summary>
        /// <param name="msg">The message that describes the error.</param>
        public QueueFullException(string msg)
            : base(msg)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueFullException"/> class.
        /// </summary>
        /// <param name="msg">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public QueueFullException(string msg, Exception innerException)
            : base(msg, innerException)
        {
        }
    }
}
