using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Tests.Integration.Support.Factories;
using GarageFlow.Tests.Integration.Support.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GarageFlow.Tests.Integration.Integrations;

[Collection(WebApplicationFactoryStartupTestGroup.Name)]
public sealed class IntegrationOutboxStartupTests
{
    [Fact]
    public void DevelopmentHost_ShouldStartWithoutPublisherWhenOutboxIsDisabled()
    {
        using var factory = new GarageFlowWebApplicationFactory(
            $"garageflow-outbox-disabled-{Guid.NewGuid():N}",
            disableAutoMigrate: true,
            environmentName: Environments.Development,
            outboxEnabled: false);

        using var client = factory.CreateClient();

        Assert.Null(factory.Services.GetService<IWorkOrderStatusNotificationPublisher>());
        Assert.Null(factory.Services.GetService<IIntegrationOutboxProcessor>());
        Assert.DoesNotContain(
            factory.Services.GetServices<IHostedService>(),
            service => service is IntegrationOutboxPublisher);
    }

    [Fact]
    public void DevelopmentHost_ShouldRequirePublisherWhenOutboxIsEnabled()
    {
        using var factory = new GarageFlowWebApplicationFactory(
            $"garageflow-outbox-enabled-{Guid.NewGuid():N}",
            disableAutoMigrate: true,
            environmentName: Environments.Development,
            outboxEnabled: true);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(
            nameof(IWorkOrderStatusNotificationPublisher),
            exception.ToString(),
            StringComparison.Ordinal);
    }
}
