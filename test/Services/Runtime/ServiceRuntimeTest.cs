// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Runtime;

public abstract class ServiceRuntimeTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    public sealed class RegisterServiceAsync_String_FuncOfStatefulServiceContextStatefulServiceBase_TimeSpan_CancellationToken : ServiceRuntimeTest
    {
        readonly string serviceTypeName = fuzzy.String();
        readonly Func<StatefulServiceContext, StatefulServiceBase> serviceFactory = _ => null;
        readonly TimeSpan timeout = default;
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact(Explicit = true)] // TODO: SUT testability limitation. FabricRuntime is sealed with no mockable seam.
        public void RegistersStatefulServiceFactory() =>
            throw new NotImplementedException(
                "ServiceRuntime.RegisterServiceAsync calls RuntimeContext.GetOrCreateAsync, which depends on " +
                "static methods of the sealed System.Fabric.FabricRuntime. Without a mockable seam, the path " +
                "after argument validation cannot be covered without testability changes to the SUT.");

        [Fact]
        public async Task ThrowsArgumentNullExceptionWhenServiceFactoryIsNull()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => ServiceRuntime.RegisterServiceAsync(serviceTypeName, (Func<StatefulServiceContext, StatefulServiceBase>)null, timeout, cancellationToken));
            Assert.Equal(nameof(serviceFactory), exception.ParamName);
        }

        [Fact]
        public async Task ThrowsArgumentNullExceptionWhenServiceTypeNameIsNull()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => ServiceRuntime.RegisterServiceAsync(null, serviceFactory, timeout, cancellationToken));
            Assert.Equal(nameof(serviceTypeName), exception.ParamName);
        }

        [Fact]
        public async Task ThrowsArgumentExceptionWhenServiceTypeNameIsEmpty()
        {
            var exception = await Assert.ThrowsAnyAsync<ArgumentException>(
                () => ServiceRuntime.RegisterServiceAsync(string.Empty, serviceFactory, timeout, cancellationToken));
            Assert.Equal(nameof(serviceTypeName), exception.ParamName);
        }

        [Fact]
        public async Task ThrowsArgumentExceptionWhenServiceTypeNameIsWhiteSpace()
        {
            var exception = await Assert.ThrowsAnyAsync<ArgumentException>(
                () => ServiceRuntime.RegisterServiceAsync("   ", serviceFactory, timeout, cancellationToken));
            Assert.Equal(nameof(serviceTypeName), exception.ParamName);
        }
    }

    public sealed class RegisterServiceAsync_String_FuncOfStatelessServiceContextStatelessService_TimeSpan_CancellationToken : ServiceRuntimeTest
    {
        readonly string serviceTypeName = fuzzy.String();
        readonly Func<StatelessServiceContext, StatelessService> serviceFactory = _ => null;
        readonly TimeSpan timeout = default;
        readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        [Fact(Explicit = true)] // TODO: SUT testability limitation. FabricRuntime is sealed with no mockable seam.
        public void RegistersStatelessServiceFactory() =>
            throw new NotImplementedException(
                "ServiceRuntime.RegisterServiceAsync calls RuntimeContext.GetOrCreateAsync, which depends on " +
                "static methods of the sealed System.Fabric.FabricRuntime. Without a mockable seam, the path " +
                "after argument validation cannot be covered without testability changes to the SUT.");

        [Fact]
        public async Task ThrowsArgumentNullExceptionWhenServiceFactoryIsNull()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => ServiceRuntime.RegisterServiceAsync(serviceTypeName, (Func<StatelessServiceContext, StatelessService>)null, timeout, cancellationToken));
            Assert.Equal(nameof(serviceFactory), exception.ParamName);
        }

        [Fact]
        public async Task ThrowsArgumentNullExceptionWhenServiceTypeNameIsNull()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => ServiceRuntime.RegisterServiceAsync(null, serviceFactory, timeout, cancellationToken));
            Assert.Equal(nameof(serviceTypeName), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Stateless overload calls ThrowIfNull instead of ThrowIfNullOrWhiteSpace.
        public async Task ThrowsArgumentExceptionWhenServiceTypeNameIsEmpty()
        {
            // The stateful overload validates serviceTypeName with Requires.ThrowIfNullOrWhiteSpace, but the
            // stateless overload only calls Requires.ThrowIfNull, so empty strings fall through into
            // RuntimeContext.GetOrCreateAsync instead of failing fast at the argument boundary.
            var exception = await Assert.ThrowsAnyAsync<ArgumentException>(
                () => ServiceRuntime.RegisterServiceAsync(string.Empty, serviceFactory, timeout, cancellationToken));
            Assert.Equal(nameof(serviceTypeName), exception.ParamName);
        }

        [Fact(Explicit = true)] // TODO: SUT bug. Stateless overload calls ThrowIfNull instead of ThrowIfNullOrWhiteSpace.
        public async Task ThrowsArgumentExceptionWhenServiceTypeNameIsWhiteSpace()
        {
            // The stateful overload validates serviceTypeName with Requires.ThrowIfNullOrWhiteSpace, but the
            // stateless overload only calls Requires.ThrowIfNull, so whitespace strings fall through into
            // RuntimeContext.GetOrCreateAsync instead of failing fast at the argument boundary.
            var exception = await Assert.ThrowsAnyAsync<ArgumentException>(
                () => ServiceRuntime.RegisterServiceAsync("   ", serviceFactory, timeout, cancellationToken));
            Assert.Equal(nameof(serviceTypeName), exception.ParamName);
        }
    }
}
