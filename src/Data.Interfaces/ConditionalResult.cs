// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    /// <summary>
    /// Represents the result of a Reliable Collections operation that may or may not return a value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value returned by this <see cref="ConditionalValue{TValue}"/>.</typeparam>
    public struct ConditionalValue<TValue>
    {
        /// <summary>
        /// Is there a value.
        /// </summary>
        private readonly bool hasValue;

        /// <summary>
        /// The value.
        /// </summary>
        private readonly TValue value;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConditionalValue{TValue}"/> struct with the given value.
        /// </summary>
        /// <param name="hasValue"><see langword="true"/> to indicate the value is valid; otherwise, <see langword="false"/>.</param>
        /// <param name="value">The value.</param>
        public ConditionalValue(bool hasValue, TValue value)
        {
            this.hasValue = hasValue;
            this.value = value;
        }

        /// <summary>
        /// Gets a value indicating whether the current <see cref="ConditionalValue{TValue}"/> object has a valid value of its underlying type.
        /// </summary>
        /// <value><see langword="true"/> if the value is valid; otherwise, <see langword="false"/>.</value>
        public bool HasValue
        {
            get
            {
                return this.hasValue;
            }
        }

        /// <summary>
        /// Gets the value of the current <see cref="ConditionalValue{TValue}"/> object if it has been assigned a valid underlying value.
        /// </summary>
        /// <value>The value of the object. If <see cref="HasValue"/> is <see langword="false"/>, the default value for type of the <typeparamref name="TValue"/> parameter.</value>
        public TValue Value
        {
            get
            {
                return this.value;
            }
        }
    }
}
