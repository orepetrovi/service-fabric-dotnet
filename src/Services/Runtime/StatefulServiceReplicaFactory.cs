// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Services.Runtime
{
    using System;
    using System.Fabric;

    internal class StatefulServiceReplicaFactory : IStatefulServiceFactory, IDisposable
    {
        private readonly Func<StatefulServiceContext, StatefulServiceBase> serviceFactory;
        private readonly RuntimeContext runtimeContext;

        public StatefulServiceReplicaFactory(
            RuntimeContext runtimeContext,
            Func<StatefulServiceContext, StatefulServiceBase> serviceFactory)
        {
            this.serviceFactory = serviceFactory ?? throw new ArgumentNullException(nameof(serviceFactory));
            this.runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
        }

        IStatefulServiceReplica IStatefulServiceFactory.CreateReplica(
            string serviceTypeName,
            Uri serviceName,
            byte[] initializationData,
            Guid partitionId,
            long replicaId)
        {
            var serviceContext = new StatefulServiceContext(
                this.runtimeContext.NodeContext,
                this.runtimeContext.CodePackageContext,
                serviceTypeName,
                serviceName,
                initializationData,
                partitionId,
                replicaId);

            StatefulServiceBase service = this.serviceFactory(serviceContext) ?? throw new InvalidOperationException($"{nameof(serviceFactory)} returned null");
            return new StatefulServiceReplicaAdapter(service.Context, service);
        }

        public void Dispose()
        {
            this.runtimeContext.Dispose();
        }
    }
}
