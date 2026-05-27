// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using Xunit;

namespace Microsoft.ServiceFabric.Services;

public abstract class ServiceNameFormatTest
{
    public sealed class GetEndpointName : ServiceNameFormatTest
    {
        [Fact]
        public void AppendsEndpointSuffixToNameDerivedFromTypeName() =>
            Assert.Equal("ObjectServiceEndpoint", ServiceNameFormat.GetEndpointName(typeof(object)));

        [Fact]
        public void StripsLeadingIFromInterfaceTypeNameAndAppendsEndpointSuffix() =>
            Assert.Equal("DisposableServiceEndpoint", ServiceNameFormat.GetEndpointName(typeof(IDisposable)));

        // Pins current behavior: SUT does not validate the argument and dereferences it. Bug fix is out of scope.
        [Fact]
        public void ThrowsNullReferenceExceptionWhenServiceInterfaceTypeIsNull() =>
            Assert.Throws<NullReferenceException>(() => ServiceNameFormat.GetEndpointName(null));
    }

    public sealed class GetName_String : ServiceNameFormatTest
    {
        [Theory]
        [InlineData("Foo", "FooService")]
        [InlineData("MyService", "MyService")]
        [InlineData("myservice", "myservice")]
        [InlineData("MYSERVICE", "MYSERVICE")]
        [InlineData("IFoo", "FooService")]
        [InlineData("IMyService", "MyService")]
        [InlineData("IService", "Service")]
        [InlineData("Ifoo", "IfooService")]
        [InlineData("iservice", "iservice")]
        [InlineData("", "Service")]
        [InlineData("I", "Service")]
        public void ReturnsExpectedName(string serviceInterfaceTypeName, string expected) =>
            Assert.Equal(expected, ServiceNameFormat.GetName(serviceInterfaceTypeName));

        // Pins current behavior: SUT does not validate the argument and dereferences it. Bug fix is out of scope.
        [Fact]
        public void ThrowsNullReferenceExceptionWhenServiceInterfaceTypeNameIsNull() =>
            Assert.Throws<NullReferenceException>(() => ServiceNameFormat.GetName((string)null));
    }

    public sealed class GetName_Type : ServiceNameFormatTest
    {
        [Fact]
        public void ReturnsNameDerivedFromTypeName() =>
            Assert.Equal("ObjectService", ServiceNameFormat.GetName(typeof(object)));

        [Fact]
        public void StripsLeadingIFromInterfaceTypeName() =>
            Assert.Equal("DisposableService", ServiceNameFormat.GetName(typeof(IDisposable)));

        // Pins current behavior: SUT does not validate the argument and dereferences it. Bug fix is out of scope.
        [Fact]
        public void ThrowsNullReferenceExceptionWhenServiceInterfaceTypeIsNull() =>
            Assert.Throws<NullReferenceException>(() => ServiceNameFormat.GetName((Type)null));
    }
}
