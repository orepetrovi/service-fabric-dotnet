// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Common;

public abstract class IdUtilTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    public sealed class ComputeId_MethodInfo : IdUtilTest
    {
        readonly MethodInfo methodInfo = typeof(SampleType).GetMethod(nameof(SampleType.SampleMethod), BindingFlags.NonPublic | BindingFlags.Instance);

        [Fact]
        public void CombinesMethodNameWithDeclaringTypeNamespaceAndNameWhenAllPresent()
        {
            int expected = Combine(
                methodInfo.DeclaringType.Name.GetHashCode(),
                Combine(methodInfo.DeclaringType.Namespace.GetHashCode(), methodInfo.Name.GetHashCode()));

            Assert.Equal(expected, IdUtil.ComputeId(methodInfo));
        }

        [Fact]
        public void CombinesMethodNameWithDeclaringTypeNameWhenDeclaringTypeNamespaceIsNull()
        {
            MethodInfo m = typeof(IdUtilTest_GlobalNamespaceType).GetMethod(nameof(IdUtilTest_GlobalNamespaceType.Method), BindingFlags.NonPublic | BindingFlags.Instance);

            int expected = Combine(m.DeclaringType.Name.GetHashCode(), m.Name.GetHashCode());

            Assert.Null(m.DeclaringType.Namespace);
            Assert.Equal(expected, IdUtil.ComputeId(m));
        }

        [Fact]
        public void ReturnsMethodNameHashWhenDeclaringTypeIsNull()
        {
            MethodInfo dyn = NewDynamicMethod();

            Assert.Null(dyn.DeclaringType);
            Assert.Equal(dyn.Name.GetHashCode(), IdUtil.ComputeId(dyn));
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenMethodInfoIsNull() =>
            Assert.Equal(nameof(methodInfo), Assert.Throws<ArgumentNullException>(() => IdUtil.ComputeId((MethodInfo)null)).ParamName);
    }

    public sealed class ComputeId_String_String : IdUtilTest
    {
        readonly string typeName = fuzzy.String();
        readonly string typeNamespace = fuzzy.String();

        [Fact]
        public void CombinesNameAndNamespaceWhenNamespaceIsNotNull()
        {
            int expected = Combine(typeNamespace.GetHashCode(), typeName.GetHashCode());
            Assert.Equal(expected, IdUtil.ComputeId(typeName, typeNamespace));
        }

        [Fact]
        public void ReturnsNameHashWhenNamespaceIsNull() =>
            Assert.Equal(typeName.GetHashCode(), IdUtil.ComputeId(typeName, null));

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenTypeNameIsNull() =>
            Assert.Equal(nameof(typeName), Assert.Throws<ArgumentNullException>(() => IdUtil.ComputeId(null, typeNamespace)).ParamName);
    }

    public sealed class ComputeId_Type : IdUtilTest
    {
        readonly Type type = typeof(SampleType);

        [Fact]
        public void CombinesNameAndNamespaceWhenNamespaceIsNotNull()
        {
            int expected = Combine(type.Namespace.GetHashCode(), type.Name.GetHashCode());
            Assert.Equal(expected, IdUtil.ComputeId(type));
        }

        [Fact]
        public void ReturnsNameHashWhenNamespaceIsNull()
        {
            Type t = typeof(IdUtilTest_GlobalNamespaceType);
            Assert.Null(t.Namespace);
            Assert.Equal(t.Name.GetHashCode(), IdUtil.ComputeId(t));
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenTypeIsNull() =>
            Assert.Equal(nameof(type), Assert.Throws<ArgumentNullException>(() => IdUtil.ComputeId((Type)null)).ParamName);
    }

    public sealed class ComputeIdWithCRC_MethodInfo : IdUtilTest
    {
        readonly MethodInfo methodInfo = typeof(SampleType).GetMethod(nameof(SampleType.SampleMethod), BindingFlags.NonPublic | BindingFlags.Instance);

        [Fact]
        public void ReturnsCrcOfDeclaringTypeNameConcatenatedWithDeclaringTypeNamespaceAndMethodNameWhenAllPresent()
        {
            int expected = Crc(methodInfo.DeclaringType.Name + (methodInfo.DeclaringType.Namespace + methodInfo.Name));
            Assert.Equal(expected, IdUtil.ComputeIdWithCRC(methodInfo));
        }

        [Fact]
        public void ReturnsCrcOfDeclaringTypeNameAndMethodNameWhenDeclaringTypeNamespaceIsNull()
        {
            MethodInfo m = typeof(IdUtilTest_GlobalNamespaceType).GetMethod(nameof(IdUtilTest_GlobalNamespaceType.Method), BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.Null(m.DeclaringType.Namespace);
            Assert.Equal(Crc(m.DeclaringType.Name + m.Name), IdUtil.ComputeIdWithCRC(m));
        }

        [Fact]
        public void ReturnsCrcOfMethodNameWhenDeclaringTypeIsNull()
        {
            MethodInfo dyn = NewDynamicMethod();
            Assert.Null(dyn.DeclaringType);
            Assert.Equal(Crc(dyn.Name), IdUtil.ComputeIdWithCRC(dyn));
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenMethodInfoIsNull() =>
            Assert.Equal(nameof(methodInfo), Assert.Throws<ArgumentNullException>(() => IdUtil.ComputeIdWithCRC((MethodInfo)null)).ParamName);
    }

    public sealed class ComputeIdWithCRC_String : IdUtilTest
    {
        // "café" encodes to UTF-8 bytes 63-61-66-C3-A9 (ASCII would drop the 'é'); pinning the expected id anchors both
        // the encoding choice and the ulong-to-int conversion independently of the SUT's collaborators.
        readonly string typeName = "café";
        const int ExpectedId = unchecked((int)0xA56ADE9A);

        [Fact]
        public void ReturnsCrc64OfUtf8Bytes() =>
            Assert.Equal(ExpectedId, IdUtil.ComputeIdWithCRC(typeName));

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenTypeNameIsNull() =>
            Assert.Equal(nameof(typeName), Assert.Throws<ArgumentNullException>(() => IdUtil.ComputeIdWithCRC((string)null)).ParamName);
    }

    public sealed class ComputeIdWithCRC_Type : IdUtilTest
    {
        readonly Type type = typeof(SampleType);

        [Fact]
        public void ReturnsCrcOfNamespaceConcatenatedWithNameWhenNamespaceIsNotNull() =>
            Assert.Equal(Crc(type.Namespace + type.Name), IdUtil.ComputeIdWithCRC(type));

        [Fact]
        public void ReturnsCrcOfNameWhenNamespaceIsNull()
        {
            Type t = typeof(IdUtilTest_GlobalNamespaceType);
            Assert.Null(t.Namespace);
            Assert.Equal(Crc(t.Name), IdUtil.ComputeIdWithCRC(t));
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Missing argument validation.
        public void ThrowsArgumentNullExceptionWhenTypeIsNull() =>
            Assert.Equal(nameof(type), Assert.Throws<ArgumentNullException>(() => IdUtil.ComputeIdWithCRC((Type)null)).ParamName);
    }

    public sealed class HashCombine : IdUtilTest
    {
        readonly int newKey = fuzzy.Int32();
        readonly int currentKey = fuzzy.Int32();

        [Fact]
        public void ReturnsCurrentKeyMultipliedByConstantPlusNewKey()
        {
            int expected = unchecked((currentKey * (int)0xA5555529) + newKey);
            Assert.Equal(expected, IdUtil.HashCombine(newKey, currentKey));
        }
    }

    static int Combine(int newKey, int currentKey) =>
        unchecked((currentKey * (int)0xA5555529) + newKey);

    static int Crc(string s) => (int)CRC64.ToCRC64(Encoding.UTF8.GetBytes(s));

    static MethodInfo NewDynamicMethod() =>
        new DynamicMethod("DynMethod_" + fuzzy.String().LettersOrDigits(), typeof(void), Type.EmptyTypes);

    sealed class SampleType
    {
        internal void SampleMethod() { }
    }
}
