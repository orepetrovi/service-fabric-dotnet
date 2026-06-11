// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Data
{
    using System;

    /// <summary>
    /// Wraps a <see cref="string"/> to use <see cref="StringComparison.Ordinal"/> for <see cref="IComparable{T}"/> and <see cref="IEquatable{T}"/> interface implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="OrdinalString"/> supports explicit conversion from <see cref="OrdinalString"/> to <see cref="string"/> and implicit conversion from <see cref="string"/> to <see cref="OrdinalString"/>.
    /// This implicit conversion from <see cref="string"/> to <see cref="OrdinalString"/> minimizes code changes when upstream code uses <see cref="string"/>.
    /// Conversion from <see cref="OrdinalString"/> to <see cref="string"/> is explicit to keep comparison behavior well-defined.
    /// </para>
    /// <para>
    /// The wrapped <see cref="string"/> can be <see langword="null"/>, which is the value of <c>default(OrdinalString)</c>. Conversions and <see cref="ToString"/> return the wrapped <see langword="null"/> unchanged, while equality and comparison apply <see cref="StringComparison.Ordinal"/> rules to a <see langword="null"/> value. <see cref="GetHashCode"/> throws because it dereferences the value.
    /// </para>
    /// </remarks>
    public struct OrdinalString : IEquatable<OrdinalString>, IComparable<OrdinalString>
    {
        private readonly string value;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrdinalString"/> struct.
        /// </summary>
        /// <param name="value">The value to wrap. May be <see langword="null"/>.</param>
        public OrdinalString(string value)
        {
            this.value = value;
        }

        /// <summary>
        /// Defines an explicit conversion of a given <see cref="OrdinalString"/> to the wrapped <see cref="string"/>, which may be <see langword="null"/>.
        /// </summary>
        public static explicit operator string(OrdinalString value)
        {
            return value.value;
        }

        /// <summary>
        /// Defines an implicit conversion of a given <see cref="string"/>, which may be <see langword="null"/>, to an <see cref="OrdinalString"/>.
        /// </summary>
        public static implicit operator OrdinalString(string value)
        {
            return new OrdinalString(value);
        }

        /// <summary>
        /// Determines whether two specified <see cref="OrdinalString"/> values have the same value.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value of <paramref name="left"/> is the same as the value of <paramref name="right"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator ==(OrdinalString left, OrdinalString right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two specified <see cref="OrdinalString"/> values have different values.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value of <paramref name="left"/> is different from the value of <paramref name="right"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator !=(OrdinalString left, OrdinalString right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines whether the first <see cref="OrdinalString"/> is less than the second <see cref="OrdinalString"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value of <paramref name="left"/> is less than the value of <paramref name="right"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator <(OrdinalString left, OrdinalString right)
        {
            return left.CompareTo(right) < 0;
        }

        /// <summary>
        /// Determines whether the first <see cref="OrdinalString"/> is greater than the second <see cref="OrdinalString"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value of <paramref name="left"/> is greater than the value of <paramref name="right"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator >(OrdinalString left, OrdinalString right)
        {
            return left.CompareTo(right) > 0;
        }

        /// <summary>
        /// Determines whether the first <see cref="OrdinalString"/> is less than or equal to the second <see cref="OrdinalString"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value of <paramref name="left"/> is less than or equal to the value of <paramref name="right"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator <=(OrdinalString left, OrdinalString right)
        {
            return left.CompareTo(right) <= 0;
        }

        /// <summary>
        /// Determines whether the first <see cref="OrdinalString"/> is greater than or equal to the second <see cref="OrdinalString"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value of <paramref name="left"/> is greater than or equal to the value of <paramref name="right"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator >=(OrdinalString left, OrdinalString right)
        {
            return left.CompareTo(right) >= 0;
        }

        /// <inheritdoc/>
        /// <remarks>Returns <see langword="null"/> when the wrapped <see cref="string"/> is <see langword="null"/>.</remarks>
        public override string ToString()
        {
            return this.value;
        }

        /// <inheritdoc/>
        /// <remarks>Two <see langword="null"/> wrapped values are equal.</remarks>
        public bool Equals(OrdinalString value)
        {
            return string.Equals(this.value, value.value, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is OrdinalString other)
            {
                return this.Equals(other);
            }

            return false;
        }

        /// <inheritdoc/>
        /// <exception cref="NullReferenceException">
        /// The <see cref="OrdinalString"/> is <c>default(OrdinalString)</c> or was created from a <see langword="null"/> <see cref="string"/>.
        /// </exception>
        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        /// <inheritdoc/>
        /// <remarks>A <see langword="null"/> wrapped value sorts before any non-<see langword="null"/> value.</remarks>
        public int CompareTo(OrdinalString other)
        {
            return string.CompareOrdinal(this.value, other.value);
        }
    }
}