// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
#if !NET
using System.CodeDom;
using System.Collections.ObjectModel;
using System.Reflection;
#endif
using System.Runtime.Serialization;
using Fuzzy;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Remoting.V2.Client;
using Microsoft.ServiceFabric.Actors.Tests;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Actors.Remoting;

public abstract class ActorDataContractSurrogateTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

#if NET
    readonly ISerializationSurrogateProvider sut = ActorDataContractSurrogate.Instance;
#else
    readonly IDataContractSurrogate sut = ActorDataContractSurrogate.Instance;
#endif

#if !NET
    public sealed class GetCustomDataToExport_MemberInfo_Type : ActorDataContractSurrogateTest
    {
        [Fact]
        public void ThrowsNotImplementedException() =>
            Assert.Throws<NotImplementedException>(() => sut.GetCustomDataToExport(typeof(object).GetMethod(nameof(object.ToString)), fuzzy.Type()));
    }

    public sealed class GetCustomDataToExport_Type_Type : ActorDataContractSurrogateTest
    {
        [Fact]
        public void ThrowsNotImplementedException() =>
            Assert.Throws<NotImplementedException>(() => sut.GetCustomDataToExport(fuzzy.Type(), fuzzy.Type()));
    }

    public sealed class GetDataContractType_Type : ActorDataContractSurrogateTest
    {
        [Fact]
        public void ReturnsActorReferenceWhenTypeImplementsIActor() =>
            Assert.Same(typeof(ActorReference), sut.GetDataContractType(typeof(IFactoryTestActor)));

        [Fact]
        public void ReturnsInputTypeWhenItDoesNotImplementIActor() =>
            Assert.Same(typeof(string), sut.GetDataContractType(typeof(string)));
    }
#endif

    public sealed class GetDeserializedObject_Object_Type : ActorDataContractSurrogateTest
    {
        [Fact]
        public void ReturnsNullWhenObjIsNull() =>
            Assert.Null(sut.GetDeserializedObject(null, fuzzy.Type()));

        [Fact]
        public void ReturnsBindResultWhenObjImplementsIActorReferenceAndTargetTypeImplementsIActor()
        {
            var bound = new object();
            var reference = new Mock<IActorReference>();
            _ = reference.Setup(r => r.Bind(typeof(IFactoryTestActor))).Returns(bound);

            object result = sut.GetDeserializedObject(reference.Object, typeof(IFactoryTestActor));

            Assert.Same(bound, result);
        }

        [Fact]
        public void ReturnsObjWhenTargetTypeImplementsIActorReference()
        {
            IActorReference reference = Mock.Of<IActorReference>();
            Assert.Same(reference, sut.GetDeserializedObject(reference, typeof(IActorAndReference)));
        }

        [Fact]
        public void ReturnsObjWhenTargetTypeDoesNotImplementIActor()
        {
            IActorReference reference = Mock.Of<IActorReference>();
            Assert.Same(reference, sut.GetDeserializedObject(reference, typeof(object)));
        }

        [Fact]
        public void ReturnsObjWhenItDoesNotImplementIActorReference()
        {
            var obj = new object();
            Assert.Same(obj, sut.GetDeserializedObject(obj, fuzzy.Type()));
        }

        interface IActorAndReference : IActor, IActorReference { }
    }

#if !NET
    public sealed class GetKnownCustomDataTypes_Collection : ActorDataContractSurrogateTest
    {
        [Fact]
        public void LeavesCollectionUnchanged()
        {
            Type expected = fuzzy.Type();
            var types = new Collection<Type> { expected };

            sut.GetKnownCustomDataTypes(types);

            Assert.Single(types, expected);
        }
    }
#endif

    public sealed class GetObjectToSerialize_Object_Type : ActorDataContractSurrogateTest
    {
        [Fact]
        public void ReturnsNullWhenObjIsNull() =>
            Assert.Null(sut.GetObjectToSerialize(null, fuzzy.Type()));

        [Fact]
        public void ReturnsActorReferenceWhenObjImplementsIActor()
        {
            ActorId actorId = fuzzy.ActorId();
            var serviceUri = new Uri($"fabric:/{fuzzy.String().LettersOrDigits()}/{fuzzy.String().LettersOrDigits()}");
            string listenerName = fuzzy.String().LettersOrDigits();
            var partitionClient = new Mock<IActorServicePartitionClient>();
            _ = partitionClient.SetupGet(p => p.ServiceUri).Returns(serviceUri);
            _ = partitionClient.SetupGet(p => p.ListenerName).Returns(listenerName);
            var actorProxy = new Mock<IActorProxy>();
            _ = actorProxy.SetupGet(a => a.ActorId).Returns(actorId);
            _ = actorProxy.SetupGet(a => a.ActorServicePartitionClientV2).Returns(partitionClient.Object);
            IFactoryTestActor actor = actorProxy.As<IFactoryTestActor>().Object;

            object result = sut.GetObjectToSerialize(actor, fuzzy.Type());

            var reference = Assert.IsType<ActorReference>(result);
            Assert.Same(actorId, reference.ActorId);
            Assert.Same(serviceUri, reference.ServiceUri);
            Assert.Same(listenerName, reference.ListenerName);
        }

        [Fact]
        public void ReturnsObjUnchangedWhenItDoesNotImplementIActor()
        {
            var obj = new object();
            Assert.Same(obj, sut.GetObjectToSerialize(obj, fuzzy.Type()));
        }
    }

#if !NET
    public sealed class GetReferencedTypeOnImport_String_String_Object : ActorDataContractSurrogateTest
    {
        [Fact]
        public void ThrowsNotImplementedException() =>
            Assert.Throws<NotImplementedException>(() => sut.GetReferencedTypeOnImport(fuzzy.String(), fuzzy.String(), new object()));
    }
#endif

#if NET
    public sealed class GetSurrogateType_Type : ActorDataContractSurrogateTest
    {
        [Fact]
        public void ReturnsActorReferenceWhenTypeImplementsIActor() =>
            Assert.Same(typeof(ActorReference), sut.GetSurrogateType(typeof(IFactoryTestActor)));

        [Fact]
        public void ReturnsInputTypeWhenItDoesNotImplementIActor() =>
            Assert.Same(typeof(string), sut.GetSurrogateType(typeof(string)));
    }
#endif

    public sealed class Instance : ActorDataContractSurrogateTest
    {
        [Fact]
        public void IsNonNullActorDataContractSurrogate() =>
            Assert.IsType<ActorDataContractSurrogate>(ActorDataContractSurrogate.Instance);
    }

#if !NET
    public sealed class ProcessImportedType_CodeTypeDeclaration_CodeCompileUnit : ActorDataContractSurrogateTest
    {
        [Fact]
        public void ThrowsNotImplementedException() =>
            Assert.Throws<NotImplementedException>(() => sut.ProcessImportedType(new CodeTypeDeclaration(), new CodeCompileUnit()));
    }
#endif
}
