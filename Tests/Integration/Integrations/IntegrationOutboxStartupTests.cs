using Amazon.SimpleNotificationService;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Notifications;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Tests.Integration.Support.Factories;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:garageflow.work-orders")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:garageflow-work-orders.fifo")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:__SET_ME__")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:CONFIGURE_ME")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:TODO")]
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

        using var startupLogs = new StartupFailureLoggerProvider();
        using var observedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(startupLogs)));

        Assert.ThrowsAny<Exception>(() => observedFactory.CreateClient());

        // RunAsync can dispose the failed host before WebApplicationFactory observes
        // its startup exception. Assert the original validation failure logged by Host.
        var exception = Assert.Single(startupLogs.Exceptions.OfType<OptionsValidationException>());

        Assert.Equal(typeof(AmazonSnsStatusNotificationOptions), exception.OptionsType);
        Assert.Contains(
            AmazonSnsStatusNotificationOptions.SectionName,
            exception.Message,
            StringComparison.Ordinal);
        snsClient.VerifyNoOtherCalls();
    }
}
