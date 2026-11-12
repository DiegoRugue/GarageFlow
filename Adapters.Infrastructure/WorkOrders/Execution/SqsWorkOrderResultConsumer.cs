using Amazon.SQS;
using Amazon.SQS.Model;
using ICommand = GarageFlow.Application.Common.Messaging.ICommand;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Execution;

internal sealed partial class SqsWorkOrderResultConsumer(
    IAmazonSQS sqs,
    IServiceScopeFactory scopeFactory,
    ExecutionSchedulingQueueOptions options,
    ILogger<SqsWorkOrderResultConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var execution = PollAsync(options.ResultQueueUrl, ExecutionResultMessage.ReadCommand, stoppingToken);
        return options.BillingResultQueueUrl is { } billingQueue
            ? Task.WhenAll(execution, PollAsync(billingQueue, BillingResultMessage.ReadCommand, stoppingToken))
            : execution;
    }

    private async Task PollAsync(string queueUrl, Func<string, ICommand> decode, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 1,
                    WaitTimeSeconds = 20
                }, stoppingToken);

                if (response.Messages is { Count: > 0 })
                {
                    await ApplyAsync(response.Messages[0], queueUrl, decode, stoppingToken);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                ProcessingFailed(logger, exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    internal Task ApplyAndAcknowledgeAsync(Message message, CancellationToken cancellationToken) =>
        ApplyAsync(message, options.ResultQueueUrl, ExecutionResultMessage.ReadCommand, cancellationToken);

    internal Task ApplyBillingAndAcknowledgeAsync(Message message, CancellationToken cancellationToken) =>
        ApplyAsync(message, options.BillingResultQueueUrl
            ?? throw new InvalidOperationException("The billing result queue is not configured."),
            BillingResultMessage.ReadCommand, cancellationToken);

    private async Task ApplyAsync(
        Message message, string queueUrl, Func<string, ICommand> decode, CancellationToken cancellationToken)
    {
        var command = decode(message.Body);
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(command, cancellationToken);
        }

        await sqs.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = queueUrl,
            ReceiptHandle = message.ReceiptHandle
        }, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Work order integration result processing failed ({ErrorType}).")]
    private static partial void ProcessingFailed(ILogger logger, string errorType);
}
