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
        // Method parameters
        readonly PathString pathString;
        readonly PathString other;

        readonly PathString different;
        readonly string segment = "/" + fuzzy.Char().Between('a', 'z') + fuzzy.String().LettersOrDigits();

        public StartsWithSegments()
        {
            other = new PathString(segment);
            pathString = new PathString(segment + "/" + fuzzy.String().LettersOrDigits());
            different = new PathString("/_" + segment);
        }

        [Fact]
        public void ReturnsTrueWhenPathStringStartsWithOtherFollowedBySegmentSeparator()
        {
            bool result = PathStringExtensions.StartsWithSegments(pathString, other, out _, out _);
            Assert.True(result);
        }

        [Fact]
        public void ReturnsTrueWhenPathStringEqualsOther()
        {
            bool result = PathStringExtensions.StartsWithSegments(other, other, out _, out _);
            Assert.True(result);
        }

        [Fact]
        public void ReturnsTrueWhenPathStringMatchesOtherWithDifferentCase()
        {
            var upper = new PathString(segment.ToUpperInvariant());
            var lower = new PathString(segment.ToLowerInvariant());
            bool result = PathStringExtensions.StartsWithSegments(upper, lower, out _, out _);
            Assert.True(result);
        }

        [Fact]
        public void ReturnsFalseWhenPathStringExtendsOtherWithoutSegmentSeparator()
        {
            var extended = new PathString(segment + fuzzy.Char().Between('a', 'z') + fuzzy.String().LettersOrDigits());
            bool result = PathStringExtensions.StartsWithSegments(extended, other, out _, out _);
            Assert.False(result);
        }

        [Fact]
        public void ReturnsFalseWhenPathStringDoesNotStartWithOther()
        {
            bool result = PathStringExtensions.StartsWithSegments(different, other, out _, out _);
            Assert.False(result);
        }

        [Fact]
        public void AssignsMatchedAndRemainingPreservingCaseOfPathString()
        {
            string upperSegment = segment.ToUpperInvariant();
            string suffix = "/" + fuzzy.String().LettersOrDigits();
            var upperPathString = new PathString(upperSegment + suffix);
            var lowerOther = new PathString(segment.ToLowerInvariant());

            PathStringExtensions.StartsWithSegments(upperPathString, lowerOther, out PathString matched, out PathString remaining);

            Assert.Equal(upperSegment, matched.Value);
            Assert.Equal(suffix, remaining.Value);
        }

        [Fact]
        public void AssignsMatchedToPathStringAndRemainingToEmptyWhenPathStringEqualsOther()
        {
            PathStringExtensions.StartsWithSegments(other, other, out PathString matched, out PathString remaining);
            Assert.Equal(other, matched);
            Assert.Equal(PathString.Empty, remaining);
        }

        [Fact]
        public void AssignsMatchedAndRemainingToEmptyWhenPathStringDoesNotStartWithOther()
        {
            PathStringExtensions.StartsWithSegments(different, other, out PathString matched, out PathString remaining);
            Assert.Equal(PathString.Empty, matched);
            Assert.Equal(PathString.Empty, remaining);
        }

        [Fact]
        public void ReturnsTrueWhenPathStringAndOtherAreBothEmpty() =>
            Assert.True(PathStringExtensions.StartsWithSegments(default, default, out _, out _));

        [Fact]
        public void AssignsMatchedAndRemainingToEmptyWhenPathStringAndOtherAreBothEmpty()
        {
            PathStringExtensions.StartsWithSegments(default, default, out PathString matched, out PathString remaining);
            Assert.Equal(PathString.Empty, matched);
            Assert.Equal(PathString.Empty, remaining);
        }

        [Fact]
        public void ReturnsFalseWhenPathStringIsEmptyAndOtherIsNonEmpty() =>
            Assert.False(PathStringExtensions.StartsWithSegments(default, other, out _, out _));

        [Fact]
        public void ReturnsTrueWhenOtherIsEmptyAndPathStringStartsWithSegmentSeparator() =>
            Assert.True(PathStringExtensions.StartsWithSegments(pathString, default, out _, out _));

        [Fact]
        public void AssignsMatchedToEmptyAndRemainingToPathStringWhenOtherIsEmptyAndPathStringStartsWithSegmentSeparator()
        {
            PathStringExtensions.StartsWithSegments(pathString, default, out PathString matched, out PathString remaining);
            Assert.Equal(PathString.Empty, matched);
            Assert.Equal(pathString, remaining);
        }

        [Fact]
        public void ReturnsFalseWhenOtherHasTrailingSlashAndPathStringContinuesWithoutSegmentSeparator()
        {
            var otherWithSlash = new PathString(segment + "/");
            var input = new PathString(segment + "/" + fuzzy.Char().Between('a', 'z') + fuzzy.String().LettersOrDigits());

            bool result = PathStringExtensions.StartsWithSegments(input, otherWithSlash, out _, out _);

            Assert.False(result);
        }

        [Fact]
        public void ReturnsTrueWhenOtherHasTrailingSlashAndPathStringContinuesWithSegmentSeparator()
        {
            var otherWithSlash = new PathString(segment + "/");
            var input = new PathString(segment + "//" + fuzzy.String().LettersOrDigits());

            bool result = PathStringExtensions.StartsWithSegments(input, otherWithSlash, out _, out _);

            Assert.True(result);
        }
    }
}
