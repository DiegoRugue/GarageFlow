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
    private const string SubjectPrefix = "GarageFlow work order ";
    private const int SubjectLengthLimit = 100;

    private readonly IAmazonSimpleNotificationService _client =
        client ?? throw new ArgumentNullException(nameof(client));
    private readonly AmazonSnsStatusNotificationOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    public Task PublishAsync(
        WorkOrderStatusChangedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        Validate(notification);

        var request = new PublishRequest
        {
            TopicArn = _options.TopicArn,
            Subject = $"{SubjectPrefix}{notification.CurrentStatus}",
            Message = string.Join(
                Environment.NewLine,
                $"Work order: {notification.WorkOrderId:D}",
                $"Previous status: {notification.PreviousStatus}",
                $"Current status: {notification.CurrentStatus}",
                $"Occurred at: {notification.OccurredAt:O}")
        };

        return _client.PublishAsync(request, cancellationToken);
    }

    private static void Validate(WorkOrderStatusChangedIntegrationEvent notification)
    {
        if (notification.WorkOrderId == Guid.Empty)
        {
            throw new ArgumentException("Work order ID cannot be empty.", nameof(notification));
        }

        if (string.IsNullOrWhiteSpace(notification.PreviousStatus))
        {
            throw new ArgumentException("Previous status cannot be empty or whitespace.", nameof(notification));
        }

        if (string.IsNullOrWhiteSpace(notification.CurrentStatus))
        {
            throw new ArgumentException("Current status cannot be empty or whitespace.", nameof(notification));
        }

        if (notification.OccurredAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Occurred at must be UTC.", nameof(notification));
        }

        var subject = $"{SubjectPrefix}{notification.CurrentStatus}";
        if (subject.Length >= SubjectLengthLimit || subject.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Current status produces an invalid SNS subject.",
                nameof(notification));
        }
    }
}
