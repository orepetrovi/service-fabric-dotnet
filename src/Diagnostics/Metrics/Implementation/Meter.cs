// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Fabric.Interop;
using System.Runtime.InteropServices;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation
{
    abstract class Meter : IDisposable
    {
        protected readonly IReadOnlyCollection<string> systemDimensionValues;

        readonly IFabricMeter fabricMeter;

        static Func<object, int> finalReleaseComObject = Utility.FinalReleaseComObject;
        bool disposed = false;

        internal Meter(IFabricMeter fabricMeter, IReadOnlyCollection<string> systemDimensionValues)
        {
            this.systemDimensionValues = systemDimensionValues ?? throw new ArgumentNullException(nameof(systemDimensionValues));
            this.fabricMeter = fabricMeter ?? throw new ArgumentNullException(nameof(fabricMeter));
        }

        public void Dispose()
        {
            if (!disposed)
            {
                finalReleaseComObject(fabricMeter);
                disposed = true;
            }
        }

        protected void Record(long value) => RecordViaNative(value, 0, null, null, null);

        protected long ConvertTimeSpanToLong(TimeSpan value) => (long)Math.Round(value.TotalMilliseconds);

        unsafe protected void RecordViaNative(long value, int customDimensionCount, string dimension1Value, string dimension2Value, string dimension3Value)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Meter));
            if (customDimensionCount < 0 || customDimensionCount > 3)
                throw new ArgumentOutOfRangeException(nameof(customDimensionCount));

            int dimensionCount = systemDimensionValues.Count + customDimensionCount;

            GCHandle* dimensionPins = stackalloc GCHandle[dimensionCount];
            IntPtr* dimensionValuesPointers = stackalloc IntPtr[dimensionCount];

            try
            {
                int i = 0;
                foreach (string systemDimensionValue in systemDimensionValues)
                {
                    dimensionPins[i++] = GCHandle.Alloc(systemDimensionValue, GCHandleType.Pinned);
                }

                if (customDimensionCount > 0)
                    dimensionPins[i++] = GCHandle.Alloc(dimension1Value, GCHandleType.Pinned);

                if (customDimensionCount > 1)
                    dimensionPins[i++] = GCHandle.Alloc(dimension2Value, GCHandleType.Pinned);

                if (customDimensionCount > 2)
                    dimensionPins[i++] = GCHandle.Alloc(dimension3Value, GCHandleType.Pinned);


                for (i = 0; i < dimensionCount; i++)
                {
                    // for strings, AddrOfPinnedObject() returns a pointer to the first character - https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.gchandle.addrofpinnedobject
                    dimensionValuesPointers[i] = dimensionPins[i].AddrOfPinnedObject();
                }

                fabricMeter.Record(value, (uint)dimensionCount, (IntPtr)dimensionValuesPointers);
            }
            finally
            {
                for (int i = 0; i < dimensionCount; i++)
                {
                    if (dimensionPins[i].IsAllocated)
                        dimensionPins[i].Free();
                }
            }
        }
    }
}
