using Amazon.SQS;
using Amazon.SQS.Model;
using GarageFlow.Application.WorkOrders.Ports;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Execution;

internal sealed class SqsExecutionCommandPublisher(
    IAmazonSQS sqs,
    ExecutionSchedulingQueueOptions options) : IExecutionCommandPublisher
{
    public async Task PublishAsync(string payload, CancellationToken cancellationToken)
    {
        await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = options.CommandQueueUrl,
            MessageBody = payload
        }, cancellationToken);
    }
}
