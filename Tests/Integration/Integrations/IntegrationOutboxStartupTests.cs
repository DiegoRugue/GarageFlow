using Amazon.SimpleNotificationService;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Notifications;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Tests.Integration.Support.Factories;
using GarageFlow.Tests.Integration.Support.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

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
        Assert.Null(factory.Services.GetService<IAmazonSimpleNotificationService>());
        Assert.Null(factory.Services.GetService<IIntegrationOutboxProcessor>());
        Assert.DoesNotContain(
            factory.Services.GetServices<IHostedService>(),
            service => service is IntegrationOutboxPublisher);
    }

    [Fact]
    public void DevelopmentHost_ShouldResolveSnsPublisherAndWorkerWhenOutboxIsEnabled()
    {
        var snsClient = new Mock<IAmazonSimpleNotificationService>(MockBehavior.Strict);
        using var factory = new GarageFlowWebApplicationFactory(
            $"garageflow-outbox-enabled-{Guid.NewGuid():N}",
            disableAutoMigrate: true,
            environmentName: Environments.Development,
            outboxEnabled: true,
            snsRegion: AmazonSnsStatusNotificationOptions.RequiredRegion,
            snsTopicArn: "arn:aws:sns:us-east-1:123456789012:garageflow-work-orders",
            snsClient: snsClient.Object);

        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();

        Assert.Same(snsClient.Object, factory.Services.GetRequiredService<IAmazonSimpleNotificationService>());
        Assert.IsType<AmazonSnsWorkOrderStatusNotificationPublisher>(
            scope.ServiceProvider.GetRequiredService<IWorkOrderStatusNotificationPublisher>());
        Assert.IsType<IntegrationOutboxProcessor>(
            scope.ServiceProvider.GetRequiredService<IIntegrationOutboxProcessor>());
        Assert.Contains(
            factory.Services.GetServices<IHostedService>(),
            service => service is IntegrationOutboxPublisher);
        snsClient.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("eu-west-1", "arn:aws:sns:us-east-1:123456789012:garageflow-work-orders")]
    [InlineData("us-east-1", "")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:placeholder")]
    public void DevelopmentHost_ShouldRejectInvalidSnsSettingsWhenOutboxIsEnabled(
        string region,
        string topicArn)
    {
        var snsClient = new Mock<IAmazonSimpleNotificationService>(MockBehavior.Strict);
        using var factory = new GarageFlowWebApplicationFactory(
            $"garageflow-outbox-invalid-sns-{Guid.NewGuid():N}",
            disableAutoMigrate: true,
            environmentName: Environments.Development,
            outboxEnabled: true,
            snsRegion: region,
            snsTopicArn: topicArn,
            snsClient: snsClient.Object);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(
            AmazonSnsStatusNotificationOptions.SectionName,
            exception.ToString(),
            StringComparison.Ordinal);
        snsClient.VerifyNoOtherCalls();
    }
}
