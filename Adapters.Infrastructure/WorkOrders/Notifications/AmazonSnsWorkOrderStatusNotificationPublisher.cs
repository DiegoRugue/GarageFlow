using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Application.WorkOrders.Ports;
using Microsoft.Extensions.Options;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Notifications;

public sealed class AmazonSnsWorkOrderStatusNotificationPublisher(
    IAmazonSimpleNotificationService client,
    IOptions<AmazonSnsStatusNotificationOptions> options)
    : IWorkOrderStatusNotificationPublisher
{
    private readonly IAmazonSimpleNotificationService _client =
        client ?? throw new ArgumentNullException(nameof(client));
    private readonly AmazonSnsStatusNotificationOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    public Task PublishAsync(
        WorkOrderStatusChangedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var request = new PublishRequest
        {
            TopicArn = _options.TopicArn,
            Subject = $"GarageFlow work order {notification.CurrentStatus}",
            Message = string.Join(
                Environment.NewLine,
                $"Work order: {notification.WorkOrderId:D}",
                $"Previous status: {notification.PreviousStatus}",
                $"Current status: {notification.CurrentStatus}",
                $"Occurred at: {notification.OccurredAt:O}")
        };

        return _client.PublishAsync(request, cancellationToken);
    }
}
