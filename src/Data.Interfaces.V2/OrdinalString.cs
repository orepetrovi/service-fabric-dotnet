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
    /// <see cref="OrdinalString"/> supports explicit conversion from <see cref="OrdinalString"/> to <see cref="string"/> and implicit conversion from <see cref="string"/> to <see cref="OrdinalString"/>.
    /// This implicit conversion from <see cref="string"/> to <see cref="OrdinalString"/> is implemented to help the customer minimize code change if string was used in upstream code. 
    /// However, to ensure that we have a well defined comparison behavior, we only allow explicit conversion from <see cref="OrdinalString"/> to <see cref="string"/>.
    /// </remarks>
    public struct OrdinalString : IEquatable<OrdinalString>, IComparable<OrdinalString>
    {
        private readonly string value;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrdinalString"/> struct.
        /// </summary>
        public OrdinalString(string value)
        {
            this.value = value;
        }

        /// <summary>
        /// Defines an explicit conversion of a given <see cref="OrdinalString"/> to a <see cref="string"/>.
        /// </summary>
        public static explicit operator string(OrdinalString value)
        {
            return value.value;
        }

        /// <summary>
        /// Defines an implicit conversion of a given <see cref="string"/> to an <see cref="OrdinalString"/>.
        /// </summary>
        public static implicit operator OrdinalString(string value)
        {
            return new OrdinalString(value);
        }

        /// <summary>
        /// Determines whether two specified <see cref="OrdinalString"/>s have the same value.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value of <paramref name="left"/> is the same as the value of <paramref name="right"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator ==(OrdinalString left, OrdinalString right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two specified <see cref="OrdinalString"/>s have different values.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value of <paramref name="left"/> is different from the value of <paramref name="right"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator !=(OrdinalString left, OrdinalString right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines if the first <see cref="OrdinalString"/> is smaller than the second <see cref="OrdinalString"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value of <paramref name="left"/> is smaller than the value of <paramref name="right"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator <(OrdinalString left, OrdinalString right)
        {
            return left.CompareTo(right) < 0;
        }

        /// <summary>
        /// Determines if the first <see cref="OrdinalString"/> is greater than the second <see cref="OrdinalString"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value of <paramref name="left"/> is greater than the value of <paramref name="right"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator >(OrdinalString left, OrdinalString right)
        {
            return left.CompareTo(right) > 0;
        }

        /// <summary>
        /// Determines if the first <see cref="OrdinalString"/> is less than or equal to the second <see cref="OrdinalString"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value of <paramref name="left"/> is less than or equal to the value of <paramref name="right"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator <=(OrdinalString left, OrdinalString right)
        {
            return left.CompareTo(right) <= 0;
        }

        /// <summary>
        /// Determines if the first <see cref="OrdinalString"/> is greater than or equal to the second <see cref="OrdinalString"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the value of <paramref name="left"/> is greater than or equal to the value of <paramref name="right"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator >=(OrdinalString left, OrdinalString right)
        {
            return left.CompareTo(right) >= 0;
        }

        /// <summary>
        /// Converts the value of this instance to a <see cref="string"/>.
        /// </summary>
        public override string ToString()
        {
            return this.value;
        }

        /// <summary>
        /// Determines whether this instance and another specified <see cref="OrdinalString"/> object 
        /// have the same value.
        /// </summary>
        /// <param name="value">
        /// The <see cref="OrdinalString"/> to compare to this instance.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the value of the <paramref name="value"/> parameter is the same as the value of this instance; 
        /// otherwise, <see langword="false"/>.
        /// </returns>
        public bool Equals(OrdinalString value)
        {
            return string.Equals(this.value, value.value, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether this instance and a specified <see cref="object"/>, which must also 
        /// be an <see cref="OrdinalString"/> object, have the same value.
        /// </summary>
        /// <param name="obj">
        /// The object to compare with this instance.</param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="obj"/> is an <see cref="OrdinalString"/> and its value is the same as this instance; 
        /// otherwise, <see langword="false"/>. If <paramref name="obj"/> is <see langword="null"/>, the method returns <see langword="false"/>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (obj is OrdinalString other)
            {
                return this.Equals(other);
            }

            return false;
        }

        /// <summary>
        /// Returns the hash code for this <see cref="OrdinalString"/>.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer hash code.
        /// </returns>
        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        /// <summary>
        /// Compares this instance with a specified <see cref="OrdinalString"/> object and indicates 
        /// whether this instance precedes, follows, or appears in the same position 
        /// in the sort order as the specified <see cref="OrdinalString"/>.
        /// </summary>
        /// <param name="other">
        /// The <see cref="OrdinalString"/> to compare with this instance.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that indicates whether this instance precedes, follows, or appears in the 
        /// same position in the sort order as the <paramref name="other"/> parameter.
        /// </returns>
        public int CompareTo(OrdinalString other)
        {
            return string.CompareOrdinal(this.value, other.value);
        }
    }
}
