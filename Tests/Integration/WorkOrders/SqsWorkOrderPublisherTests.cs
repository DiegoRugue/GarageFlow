using Amazon.SQS;
using Amazon.SQS.Model;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Application.WorkOrders.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace GarageFlow.Tests.Integration.WorkOrders;

public sealed class SqsWorkOrderPublisherTests
{
    [Fact]
    public async Task Phase4PublishersSendCommandsToTheirConfiguredQueues()
    {
        const string executionQueue = "https://sqs.example.test/execution-commands";
        const string billingQueue = "https://sqs.example.test/billing-commands";
        const string executionPayload = """{"messageType":"execution.schedule.v1"}""";
        const string billingPayload = """{"messageType":"billing.create-charge.v1"}""";

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Integrations:ExecutionScheduling:Enabled"] = "true",
            ["Integrations:ExecutionScheduling:CommandQueueUrl"] = executionQueue,
            ["Integrations:ExecutionScheduling:ResultQueueUrl"] = "https://sqs.example.test/execution-results",
            ["Integrations:ExecutionScheduling:BillingCommandQueueUrl"] = billingQueue,
            ["Integrations:ExecutionScheduling:BillingResultQueueUrl"] = "https://sqs.example.test/billing-results"
        }).Build();

        var sent = new List<SendMessageRequest>();
        var sqs = new Mock<IAmazonSQS>(MockBehavior.Strict);
        sqs.Setup(client => client.SendMessageAsync(
                It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((request, _) => sent.Add(request))
            .ReturnsAsync(new SendMessageResponse());

        var services = new ServiceCollection();
        services.AddGarageFlowInfrastructure(configuration,
            Mock.Of<IHostEnvironment>(environment => environment.EnvironmentName == "IntegrationTests"));
        services.AddSingleton(sqs.Object);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var executionPublisher = scope.ServiceProvider.GetRequiredService<IExecutionCommandPublisher>();
        var billingPublisher = scope.ServiceProvider.GetRequiredService<IBillingChargePublisher>();
        await executionPublisher.PublishAsync(executionPayload, CancellationToken.None);
        await billingPublisher.PublishAsync(billingPayload, CancellationToken.None);

        Assert.Collection(sent,
            request =>
            {
                Assert.Equal(executionQueue, request.QueueUrl);
                Assert.Equal(executionPayload, request.MessageBody);
            },
            request =>
            {
                Assert.Equal(billingQueue, request.QueueUrl);
                Assert.Equal(billingPayload, request.MessageBody);
            });
    }

    [Theory]
    [InlineData("BillingCommandQueueUrl")]
    [InlineData("BillingResultQueueUrl")]
    public void Phase4Startup_ShouldRejectMissingBillingQueue(string missingQueue)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Integrations:ExecutionScheduling:Enabled"] = "true",
            ["Integrations:ExecutionScheduling:CommandQueueUrl"] = "https://sqs.example.test/execution-commands",
            ["Integrations:ExecutionScheduling:ResultQueueUrl"] = "https://sqs.example.test/execution-results",
            ["Integrations:ExecutionScheduling:BillingCommandQueueUrl"] = "https://sqs.example.test/billing-commands",
            ["Integrations:ExecutionScheduling:BillingResultQueueUrl"] = "https://sqs.example.test/billing-results"
        };
        settings.Remove($"Integrations:ExecutionScheduling:{missingQueue}");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var environment = Mock.Of<IHostEnvironment>(host => host.EnvironmentName == "IntegrationTests");

        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddGarageFlowInfrastructure(configuration, environment));
    }
}
