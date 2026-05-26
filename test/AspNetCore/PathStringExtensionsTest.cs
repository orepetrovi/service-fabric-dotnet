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
        readonly string segment = "/" + fuzzy.String().LettersOrDigits();

        public StartsWithSegments()
        {
            other = new PathString(segment);
            pathString = new PathString(segment + "/" + fuzzy.String().LettersOrDigits());
            different = new PathString("/_" + segment);
        }

        [Fact]
        public void ReturnsTrueWhenPathStringStartsWithOtherFollowedBySegmentSeparator() =>
            Assert.True(PathStringExtensions.StartsWithSegments(pathString, other, out _, out _));

        [Fact]
        public void ReturnsTrueWhenPathStringEqualsOther() =>
            Assert.True(PathStringExtensions.StartsWithSegments(other, other, out _, out _));

        [Fact]
        public void ReturnsTrueWhenPathStringMatchesOtherWithDifferentCase()
        {
            var casedSegment = segment + fuzzy.Char().Between('a', 'z');
            var upper = new PathString(casedSegment.ToUpperInvariant());
            var lower = new PathString(casedSegment.ToLowerInvariant());
            Assert.True(PathStringExtensions.StartsWithSegments(upper, lower, out _, out _));
        }

        [Fact]
        public void ReturnsFalseWhenPathStringExtendsOtherWithoutSegmentSeparator()
        {
            var extended = new PathString(segment + fuzzy.Char().Between('a', 'z') + fuzzy.String().LettersOrDigits());
            Assert.False(PathStringExtensions.StartsWithSegments(extended, other, out _, out _));
        }

        [Fact]
        public void ReturnsFalseWhenPathStringDoesNotStartWithOther() =>
            Assert.False(PathStringExtensions.StartsWithSegments(different, other, out _, out _));

        [Fact]
        public void AssignsMatchedAndRemainingPreservingCaseOfPathString()
        {
            string casedSegment = segment + fuzzy.Char().Between('a', 'z');
            string upperSegment = casedSegment.ToUpperInvariant();
            string suffix = "/" + fuzzy.String().LettersOrDigits();
            var upper = new PathString(upperSegment + suffix);
            var lower = new PathString(casedSegment.ToLowerInvariant());

            PathStringExtensions.StartsWithSegments(upper, lower, out PathString matched, out PathString remaining);

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
    }
}
