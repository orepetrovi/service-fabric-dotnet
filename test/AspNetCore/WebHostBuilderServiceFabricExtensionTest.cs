// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Fabric;
using System.Linq;
using Fuzzy;
using Inspector;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Microsoft.ServiceFabric.Services.Communication.AspNetCore;

public abstract class WebHostBuilderServiceFabricExtensionTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    readonly Mock<IWebHostBuilder> hostBuilder = new();

    WebHostBuilderServiceFabricExtensionTest() =>
        _ = hostBuilder.Setup(_ => _.UseSetting(It.IsAny<string>(), It.IsAny<string>())).Returns(hostBuilder.Object);

    public sealed class UseServiceFabricIntegration : WebHostBuilderServiceFabricExtensionTest
    {
        // Method parameters
        readonly AspNetCoreCommunicationListener listener = new TestCommunicationListener(fuzzy.StatelessServiceContext());
        readonly ServiceFabricIntegrationOptions options = fuzzy.Enum<ServiceFabricIntegrationOptions>();

        sealed class TestCommunicationListener(ServiceContext serviceContext)
            : AspNetCoreCommunicationListener(serviceContext, (_, _) => Mock.Of<IWebHost>())
        {
            protected internal override string GetListenerUrl() => string.Empty;
        }

        [Fact]
        public void ReturnsHostBuilderAfterConfiguringIt()
        {
            IWebHostBuilder actual = hostBuilder.Object.UseServiceFabricIntegration(listener, options);

            Assert.Same(hostBuilder.Object, actual);
        }

        [Fact]
        public void ThrowsArgumentNullExceptionWhenHostBuilderIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => WebHostBuilderServiceFabricExtension.UseServiceFabricIntegration(null, listener, options));
            Assert.Equal(nameof(hostBuilder), exception.ParamName);
        }

        // TODO: SUT bug. UseServiceFabricIntegration does not validate listener and throws NullReferenceException.
        [Fact(Explicit = true)]
        public void ThrowsArgumentNullExceptionWhenListenerIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => hostBuilder.Object.UseServiceFabricIntegration(null, options));
            Assert.Equal(nameof(listener), exception.ParamName);
        }

        [Fact]
        public void ReturnsHostBuilderWithoutReconfiguringWhenSettingIsAlreadyTrue()
        {
            _ = hostBuilder.Setup(_ => _.GetSetting("UseServiceFabricIntegration")).Returns("True");

            IWebHostBuilder actual = hostBuilder.Object.UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.UseUniqueServiceUrl);

            Assert.Same(hostBuilder.Object, actual);
            hostBuilder.Verify(_ => _.UseSetting(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            hostBuilder.Verify(_ => _.ConfigureServices(It.IsAny<Action<IServiceCollection>>()), Times.Never);
            Assert.Empty(listener.UrlSuffix);
        }

        [Fact]
        public void MarksHostBuilderSettingToPreventDoubleConfiguration()
        {
            hostBuilder.Object.UseServiceFabricIntegration(listener, options);

            hostBuilder.Verify(_ => _.UseSetting("UseServiceFabricIntegration", "True"), Times.Once);
        }

        [Fact]
        public void RegistersServiceFabricSetupFilterAsSingletonStartupFilter()
        {
            Action<IServiceCollection> captured = null;
            _ = hostBuilder
                .Setup(_ => _.ConfigureServices(It.IsAny<Action<IServiceCollection>>()))
                .Callback<Action<IServiceCollection>>(a => captured = a)
                .Returns(hostBuilder.Object);

            hostBuilder.Object.UseServiceFabricIntegration(listener, options);

            hostBuilder.Verify(_ => _.ConfigureServices(It.IsAny<Action<IServiceCollection>>()), Times.Once);
            ServiceCollection services = new();
            captured(services);
            ServiceDescriptor descriptor = services.Single(_ => _.ServiceType == typeof(IStartupFilter));
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            ServiceFabricSetupFilter filter = Assert.IsType<ServiceFabricSetupFilter>(descriptor.ImplementationInstance);
            Assert.Equal(listener.UrlSuffix, filter.Field<string>().Value);
        }

        [Theory]
        [InlineData(ServiceFabricIntegrationOptions.None)]
        [InlineData(ServiceFabricIntegrationOptions.UseUniqueServiceUrl)]
        [InlineData(ServiceFabricIntegrationOptions.UseReverseProxyIntegration)]
        [InlineData(ServiceFabricIntegrationOptions.UseUniqueServiceUrl | ServiceFabricIntegrationOptions.UseReverseProxyIntegration)]
        public void StoresOptionsInServiceFabricSetupFilter(ServiceFabricIntegrationOptions options)
        {
            Action<IServiceCollection> captured = null;
            _ = hostBuilder
                .Setup(_ => _.ConfigureServices(It.IsAny<Action<IServiceCollection>>()))
                .Callback<Action<IServiceCollection>>(a => captured = a)
                .Returns(hostBuilder.Object);

            hostBuilder.Object.UseServiceFabricIntegration(listener, options);

            hostBuilder.Verify(_ => _.ConfigureServices(It.IsAny<Action<IServiceCollection>>()), Times.Once);
            ServiceCollection services = new();
            captured(services);
            ServiceDescriptor descriptor = services.Single(_ => _.ServiceType == typeof(IStartupFilter));
            ServiceFabricSetupFilter filter = Assert.IsType<ServiceFabricSetupFilter>(descriptor.ImplementationInstance);
            Assert.Equal(options, filter.Field<ServiceFabricIntegrationOptions>().Value);
        }

        [Theory]
        [InlineData(ServiceFabricIntegrationOptions.UseUniqueServiceUrl)]
        [InlineData(ServiceFabricIntegrationOptions.UseUniqueServiceUrl | ServiceFabricIntegrationOptions.UseReverseProxyIntegration)]
        public void ConfiguresListenerToUseUniqueServiceUrlWhenOptionsHasUseUniqueServiceUrlFlag(ServiceFabricIntegrationOptions options)
        {
            hostBuilder.Object.UseServiceFabricIntegration(listener, options);

            Assert.NotEmpty(listener.UrlSuffix);
        }

        [Theory]
        [InlineData(ServiceFabricIntegrationOptions.None)]
        [InlineData(ServiceFabricIntegrationOptions.UseReverseProxyIntegration)]
        public void DoesNotConfigureListenerToUseUniqueServiceUrlWhenOptionsDoesNotHaveUseUniqueServiceUrlFlag(ServiceFabricIntegrationOptions options)
        {
            hostBuilder.Object.UseServiceFabricIntegration(listener, options);

            Assert.Empty(listener.UrlSuffix);
        }
    }
}
