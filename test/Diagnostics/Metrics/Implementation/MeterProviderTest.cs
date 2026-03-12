// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Interop;
using Fuzzy;
using Inspector;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation
{
    public class MeterProviderTest : IDisposable
    {
        static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

        readonly IMeterProvider<int> sut;

        readonly ServiceContext serviceContext = fuzzy.ServiceContext();

        public MeterProviderTest()
        {
            typeof(MeterProvider<int>).Field<Func<IFabricMeterProvider>>().Set(() => Mock.Of<IFabricMeterProvider>());
            sut = new Mock<MeterProvider<int>>(serviceContext).Object;
        }

        public virtual void Dispose() => typeof(MeterProvider<int>).Field<Func<IFabricMeterProvider>>().Set(NativeTelemetry.FabricCreateMeterProvider);

        public class Constructor : MeterProviderTest
        {

            [Fact]
            public void SetsRequiredDimensionsFromServiceContext()
            {
                string[] actualSystemDimensionNames = (string[])sut.Private().Field<IReadOnlyCollection<string>>().Value;
                string[] actualSystemDimensionValues = (string[])sut.Protected().Field<IReadOnlyCollection<string>>().Value;

                Assert.Equal(serviceContext.PartitionId.ToString(), actualSystemDimensionValues[0]);
                Assert.Equal(serviceContext.ServiceTypeName, actualSystemDimensionValues[1]);
                Assert.Equal(serviceContext.ServiceName.ToString(), actualSystemDimensionValues[2]);
                Assert.Equal(serviceContext.CodePackageActivationContext.ApplicationName, actualSystemDimensionValues[3]);
                Assert.Equal(serviceContext.CodePackageActivationContext.ApplicationTypeName, actualSystemDimensionValues[4]);

                Assert.Equal(nameof(ServiceContext.PartitionId), actualSystemDimensionNames[0]);
                Assert.Equal(nameof(ServiceContext.ServiceTypeName), actualSystemDimensionNames[1]);
                Assert.Equal(nameof(ServiceContext.ServiceName), actualSystemDimensionNames[2]);
                Assert.Equal(nameof(ServiceContext.CodePackageActivationContext.ApplicationName), actualSystemDimensionNames[3]);
                Assert.Equal(nameof(ServiceContext.CodePackageActivationContext.ApplicationTypeName), actualSystemDimensionNames[4]);
            }

            [Fact]
            public void SetsEmptyDimensionsIfNullServiceContext()
            {
                var sut = new Mock<MeterProvider<int>>(null).Object;

                IReadOnlyCollection<string> actualSystemDimensionNames = sut.Private().Field<IReadOnlyCollection<string>>().Value;
                IReadOnlyCollection<string> actualSystemDimensionValues = sut.Protected().Field<IReadOnlyCollection<string>>().Value;

                Assert.Empty(actualSystemDimensionValues);
                Assert.Empty(actualSystemDimensionNames);
            }
        }

        public class DisposeTest : MeterProviderTest
        {
            public DisposeTest() => typeof(MeterProvider<int>).Field<Func<object, int>>().Set(objectParam => fuzzy.Int32());
            override public void Dispose()
            {
                base.Dispose();
                typeof(MeterProvider<int>).Field<Func<object, int>>().Set(Utility.FinalReleaseComObject);
            }

            [Fact]
            public void DisposesNativeFabricMeterProvider()
            {
                sut.Dispose();

                Assert.True(sut.Field<bool>().Value);
            }
        }

        public class CreateMeterProvider : DisposeTest
        {
            readonly Func<string, string, IEnumerable<string>, IFabricMeter> sutMethod;

            readonly string metricNamespace = fuzzy.String();
            readonly string metricName = fuzzy.String();
            readonly IEnumerable<string> dimensions = fuzzy.List(() => fuzzy.String());

            public CreateMeterProvider() => sutMethod = sut.Protected().Method<Func<string, string, IEnumerable<string>, IFabricMeter>>();

            [Fact]
            public void ThrowsIfDisposed()
            {
                sut.Dispose();

                Assert.Throws<ObjectDisposedException>(() => sutMethod.Invoke(metricNamespace, metricName, dimensions));
            }
        }
    }
}
