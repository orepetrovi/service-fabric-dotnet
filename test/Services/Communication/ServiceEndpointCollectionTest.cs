// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication;

public abstract class ServiceEndpointCollectionTest
{
    readonly ServiceEndpointCollection sut;

    // Constructor parameters
    readonly string listenerName = fuzzy.String();
    readonly string endpointAddress = fuzzy.String();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    ServiceEndpointCollectionTest() =>
        sut = new ServiceEndpointCollection(listenerName, endpointAddress);

    public sealed class AddEndpoint : ServiceEndpointCollectionTest
    {
        // Method parameters
        new readonly string listenerName;
        new readonly string endpointAddress = fuzzy.String();

        public AddEndpoint() => listenerName = base.listenerName + fuzzy.String();

        [Fact]
        public void AddsEndpointToCollection()
        {
            sut.AddEndpoint(listenerName, endpointAddress);

            Assert.Same(endpointAddress, sut.ToReadOnlyDictionary()[listenerName]);
        }

        [Fact(Explicit = true)]
        public void ThrowsArgumentNullExceptionWhenListenerNameIsNull()
        {
            // TODO: SUT bug. AddEndpoint does not validate listenerName; Dictionary throws with ParamName "key".
            var exception = Assert.Throws<ArgumentNullException>(() => sut.AddEndpoint(null, endpointAddress));
            Assert.Equal(nameof(listenerName), exception.ParamName);
        }

        [Fact(Explicit = true)]
        public void ThrowsArgumentNullExceptionWhenEndpointAddressIsNull()
        {
            // TODO: SUT bug. AddEndpoint does not validate endpointAddress and stores null in the dictionary.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.AddEndpoint(listenerName, null));
            Assert.Equal(nameof(endpointAddress), exception.ParamName);
        }

        [Fact]
        public void ThrowsFabricElementAlreadyExistsExceptionWhenListenerNameAlreadyExists()
        {
            _ = Assert.Throws<FabricElementAlreadyExistsException>(
                () => sut.AddEndpoint(base.listenerName, endpointAddress));
        }

        [Fact]
        public void ThrowsFabricElementAlreadyExistsExceptionWhenListenerNameIsEmptyAndAlreadyExists()
        {
            var sut = new ServiceEndpointCollection(string.Empty, endpointAddress);

            _ = Assert.Throws<FabricElementAlreadyExistsException>(
                () => sut.AddEndpoint(string.Empty, fuzzy.String()));
        }
    }

    public sealed class AddEndpoints : ServiceEndpointCollectionTest
    {
        // Method parameters
        readonly ServiceEndpointCollection newEndpoints = new();

        readonly string newListenerName;
        readonly string newEndpointAddress = fuzzy.String();

        public AddEndpoints() => newListenerName = listenerName + fuzzy.String();

        [Fact]
        public void AddsAllEndpointsFromGivenCollection()
        {
            string extraListenerName = newListenerName + fuzzy.String();
            string extraEndpointAddress = fuzzy.String();
            newEndpoints.AddEndpoint(newListenerName, newEndpointAddress);
            newEndpoints.AddEndpoint(extraListenerName, extraEndpointAddress);

            sut.AddEndpoints(newEndpoints);

            IReadOnlyDictionary<string, string> actual = sut.ToReadOnlyDictionary();
            Assert.Same(newEndpointAddress, actual[newListenerName]);
            Assert.Same(extraEndpointAddress, actual[extraListenerName]);
        }

        [Fact]
        public void ThrowsFabricElementAlreadyExistsExceptionWhenListenerNameAlreadyExists()
        {
            newEndpoints.AddEndpoint(listenerName, newEndpointAddress);

            _ = Assert.Throws<FabricElementAlreadyExistsException>(() => sut.AddEndpoints(newEndpoints));
        }

        [Fact(Explicit = true)]
        public void ThrowsArgumentNullExceptionWhenNewEndpointsIsNull()
        {
            // TODO: SUT bug. AddEndpoints dereferences newEndpoints without validation, causing NullReferenceException.
            var exception = Assert.Throws<ArgumentNullException>(() => sut.AddEndpoints(null));
            Assert.Equal(nameof(newEndpoints), exception.ParamName);
        }
    }

    public sealed class Constructor : ServiceEndpointCollectionTest
    {
        [Fact]
        public void InitializesEmptyCollection()
        {
            var sut = new ServiceEndpointCollection();
            Assert.Empty(sut.ToReadOnlyDictionary());
        }
    }

    public sealed class ToReadOnlyDictionary : ServiceEndpointCollectionTest
    {
        [Fact]
        public void ReturnsReadOnlyDictionaryWithEndpoints()
        {
            IReadOnlyDictionary<string, string> actual = sut.ToReadOnlyDictionary();

            Assert.Same(endpointAddress, actual[listenerName]);
            Assert.Single(actual);
        }
    }

    public new sealed class ToString : ServiceEndpointCollectionTest
    {
        [Fact]
        public void ReturnsJsonStringWithDocumentedFormat()
        {
            // Alphanumeric values avoid having to reimplement JSON escaping in the expected string.
            string listenerName = fuzzy.String().LettersOrDigits();
            string endpointAddress = fuzzy.String().LettersOrDigits();
            var sut = new ServiceEndpointCollection(listenerName, endpointAddress);

            Assert.Equal($"{{\"Endpoints\":{{\"{listenerName}\":\"{endpointAddress}\"}}}}", sut.ToString());
        }

        [Fact]
        public void ReturnsEmptyStringWhenCollectionIsEmpty() =>
            Assert.Equal(string.Empty, new ServiceEndpointCollection().ToString());
    }

    public sealed class TryGetEndpointAddress : ServiceEndpointCollectionTest
    {
        [Fact]
        public void ReturnsTrueAndOutputsEndpointAddressWhenListenerNameExists()
        {
            bool result = sut.TryGetEndpointAddress(listenerName, out string actual);

            Assert.True(result);
            Assert.Same(endpointAddress, actual);
        }

        [Fact]
        public void ReturnsFalseAndOutputsNullWhenListenerNameDoesNotExist()
        {
            string unknown = listenerName + fuzzy.String();

            bool result = sut.TryGetEndpointAddress(unknown, out string actual);

            Assert.False(result);
            Assert.Null(actual);
        }

        [Fact(Explicit = true)]
        public void ThrowsArgumentNullExceptionWhenListenerNameIsNull()
        {
            // TODO: SUT bug. TryGetEndpointAddress does not validate listenerName; Dictionary throws with ParamName "key".
            var exception = Assert.Throws<ArgumentNullException>(() => sut.TryGetEndpointAddress(null, out _));
            Assert.Equal(nameof(listenerName), exception.ParamName);
        }
    }

    public sealed class TryGetFirstEndpointAddress : ServiceEndpointCollectionTest
    {
        [Fact]
        public void ReturnsTrueAndOutputsFirstEndpointAddressWhenCollectionIsNotEmpty()
        {
            bool result = sut.TryGetFirstEndpointAddress(out string actual);

            Assert.True(result);
            Assert.Same(endpointAddress, actual);
        }

        [Fact]
        public void ReturnsFalseAndOutputsNullWhenCollectionIsEmpty()
        {
            var sut = new ServiceEndpointCollection();

            bool result = sut.TryGetFirstEndpointAddress(out string actual);

            Assert.False(result);
            Assert.Null(actual);
        }
    }

    public sealed class TryParseEndpointsString : ServiceEndpointCollectionTest
    {
        [Fact]
        public void ReturnsTrueAndOutputsCollectionParsedFromJsonString()
        {
            // Alphanumeric values avoid having to reimplement JSON escaping in the input string.
            string listenerName = fuzzy.String().LettersOrDigits();
            string endpointAddress = fuzzy.String().LettersOrDigits();
            string endpointsString = $"{{\"Endpoints\":{{\"{listenerName}\":\"{endpointAddress}\"}}}}";

            bool result = ServiceEndpointCollection.TryParseEndpointsString(endpointsString, out ServiceEndpointCollection actual);

            Assert.True(result);
            IReadOnlyDictionary<string, string> dictionary = actual.ToReadOnlyDictionary();
            Assert.Equal(endpointAddress, dictionary[listenerName]);
            Assert.Single(dictionary);
        }

        [Fact]
        public void ReturnsTrueAndOutputsEmptyCollectionWhenEndpointsStringIsEmpty()
        {
            bool result = ServiceEndpointCollection.TryParseEndpointsString(string.Empty, out ServiceEndpointCollection actual);

            Assert.True(result);
            Assert.Empty(actual.ToReadOnlyDictionary());
        }

        [Fact]
        public void ReturnsFalseAndOutputsNullWhenEndpointsStringIsInvalidJson()
        {
            bool result = ServiceEndpointCollection.TryParseEndpointsString(fuzzy.String(), out ServiceEndpointCollection actual);

            Assert.False(result);
            Assert.Null(actual);
        }

        [Fact]
        public void ReturnsFalseAndOutputsNullWhenEndpointsStringIsNull()
        {
            bool result = ServiceEndpointCollection.TryParseEndpointsString(null, out ServiceEndpointCollection actual);

            Assert.False(result);
            Assert.Null(actual);
        }
    }
}
