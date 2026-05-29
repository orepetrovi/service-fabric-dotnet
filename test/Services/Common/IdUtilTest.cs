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
        readonly MethodInfo methodInfo = typeof(SampleType).GetMethod(nameof(SampleType.SampleMethod));

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
            MethodInfo m = typeof(IdUtilTest_GlobalNamespaceType).GetMethod(nameof(IdUtilTest_GlobalNamespaceType.Method));

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
    }

    public sealed class ComputeIdWithCRC_MethodInfo : IdUtilTest
    {
        [Fact]
        public void ReturnsCrcOfDeclaringTypeNamespaceConcatenatedWithDeclaringTypeNameAndMethodNameWhenAllPresent()
        {
            MethodInfo m = typeof(SampleType).GetMethod(nameof(SampleType.SampleMethod));
            int expected = Crc(m.DeclaringType.Name + (m.DeclaringType.Namespace + m.Name));
            Assert.Equal(expected, IdUtil.ComputeIdWithCRC(m));
        }

        [Fact]
        public void ReturnsCrcOfDeclaringTypeNameAndMethodNameWhenDeclaringTypeNamespaceIsNull()
        {
            MethodInfo m = typeof(IdUtilTest_GlobalNamespaceType).GetMethod(nameof(IdUtilTest_GlobalNamespaceType.Method));
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
    }

    public sealed class ComputeIdWithCRC_String : IdUtilTest
    {
        readonly string typeName = fuzzy.String();

        [Fact]
        public void ReturnsCrc64OfUtf8Bytes() =>
            Assert.Equal((int)CRC64.ToCRC64(Encoding.UTF8.GetBytes(typeName)), IdUtil.ComputeIdWithCRC(typeName));
    }

    public sealed class ComputeIdWithCRC_Type : IdUtilTest
    {
        [Fact]
        public void ReturnsCrcOfNamespaceConcatenatedWithNameWhenNamespaceIsNotNull()
        {
            Type t = typeof(SampleType);
            Assert.Equal(Crc(t.Namespace + t.Name), IdUtil.ComputeIdWithCRC(t));
        }

        [Fact]
        public void ReturnsCrcOfNameWhenNamespaceIsNull()
        {
            Type t = typeof(IdUtilTest_GlobalNamespaceType);
            Assert.Null(t.Namespace);
            Assert.Equal(Crc(t.Name), IdUtil.ComputeIdWithCRC(t));
        }
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
        public void SampleMethod() { }
    }
}
