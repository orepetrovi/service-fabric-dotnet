// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using Fuzzy;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.AspNetCore;

public abstract class PathStringExtensionsTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    public sealed class StartsWithSegments : PathStringExtensionsTest
    {
        readonly string segment = "/" + fuzzy.Char().Between('a', 'z') + fuzzy.String().LettersOrDigits();
        readonly PathString pathString;
        readonly PathString other;

        public StartsWithSegments()
        {
            other = new PathString(segment);
            pathString = new PathString(segment + "/" + fuzzy.String().LettersOrDigits());
        }

        [Fact]
        public void ReturnsTrueWhenPathStringStartsWithOtherFollowedBySegmentSeparator()
        {
            bool result = pathString.StartsWithSegments(other, out _, out _);
            Assert.True(result);
        }

        [Fact]
        public void ReturnsTrueWhenPathStringEqualsOther()
        {
            bool result = other.StartsWithSegments(other, out _, out _);
            Assert.True(result);
        }

        [Fact]
        public void ReturnsTrueWhenPathStringMatchesOtherWithDifferentCase()
        {
            var upper = new PathString(segment.ToUpperInvariant());
            var lower = new PathString(segment.ToLowerInvariant());
            bool result = upper.StartsWithSegments(lower, out _, out _);
            Assert.True(result);
        }

        [Fact]
        public void ReturnsFalseWhenPathStringDoesNotStartWithOther()
        {
            var different = new PathString("/_" + segment);
            bool result = different.StartsWithSegments(other, out _, out _);
            Assert.False(result);
        }

        [Fact]
        public void ReturnsFalseWhenPathStringExtendsOtherWithoutSegmentSeparator()
        {
            var extended = new PathString(segment + fuzzy.String().LettersOrDigits());
            bool result = extended.StartsWithSegments(other, out _, out _);
            Assert.False(result);
        }

        [Fact]
        public void AssignsMatchedToPrefixOfPathStringPreservingItsCase()
        {
            string upperSegment = segment.ToUpperInvariant();
            var pathString = new PathString(upperSegment + "/" + fuzzy.String().LettersOrDigits());
            var other = new PathString(segment.ToLowerInvariant());

            pathString.StartsWithSegments(other, out PathString matched, out _);

            Assert.Equal(upperSegment, matched.Value);
        }

        [Fact]
        public void AssignsRemainingToSuffixOfPathStringAfterMatched()
        {
            string suffix = "/" + fuzzy.String().LettersOrDigits();
            var pathString = new PathString(segment + suffix);

            pathString.StartsWithSegments(other, out _, out PathString remaining);

            Assert.Equal(suffix, remaining.Value);
        }

        [Fact]
        public void AssignsMatchedToPathStringWhenPathStringEqualsOther()
        {
            other.StartsWithSegments(other, out PathString matched, out _);
            Assert.Equal(other, matched);
        }

        [Fact]
        public void AssignsRemainingToEmptyWhenPathStringEqualsOther()
        {
            other.StartsWithSegments(other, out _, out PathString remaining);
            Assert.Equal(PathString.Empty, remaining);
        }

        [Fact]
        public void AssignsMatchedToEmptyWhenNotMatched()
        {
            var different = new PathString("/_" + segment);
            different.StartsWithSegments(other, out PathString matched, out _);
            Assert.Equal(PathString.Empty, matched);
        }

        [Fact]
        public void AssignsRemainingToEmptyWhenNotMatched()
        {
            var different = new PathString("/_" + segment);
            different.StartsWithSegments(other, out _, out PathString remaining);
            Assert.Equal(PathString.Empty, remaining);
        }

        [Fact]
        public void ReturnsTrueWhenPathStringAndOtherAreBothEmpty() =>
            Assert.True(default(PathString).StartsWithSegments(default, out _, out _));
    }
}
