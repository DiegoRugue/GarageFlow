using Amazon.SQS;
using Amazon.SQS.Model;
using GarageFlow.Application.WorkOrders.Ports;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Execution;

internal sealed class SqsBillingChargePublisher(IAmazonSQS sqs, string queueUrl) : IBillingChargePublisher
{
    public async Task PublishAsync(string payload, CancellationToken cancellationToken)
    {
        await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = payload
        }, cancellationToken);
    }
}
