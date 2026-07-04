using GarageFlow.Application.Common.Events;
using GarageFlow.Application.WorkOrders.Abstractions;
using GarageFlow.Domain.WorkOrders.Events;
using Mediator;
using Microsoft.Extensions.Logging;

namespace GarageFlow.Application.WorkOrders.Events;

public sealed partial class SendApprovalEmailWhenEstimateWaitingApprovalRequestedHandler(
    ICustomerApprovalEmailSender emailSender,
    ILogger<SendApprovalEmailWhenEstimateWaitingApprovalRequestedHandler> logger)
    : INotificationHandler<DomainEventNotification>
{
    private readonly ICustomerApprovalEmailSender _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    private readonly ILogger<SendApprovalEmailWhenEstimateWaitingApprovalRequestedHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask Handle(DomainEventNotification notification, CancellationToken cancellationToken)
    {
        if (notification.DomainEvent is not EstimateWaitingApprovalRequested domainEvent)
        {
            return;
        }

        try
        {
            await _emailSender.SendEstimateWaitingApprovalAsync(
                domainEvent.WorkOrderId.Value,
                domainEvent.EstimateId.Value,
                domainEvent.CustomerId.Value,
                cancellationToken);
        }
        catch (Exception exception)
        {
            LogApprovalEmailFailure(
                _logger,
                domainEvent.WorkOrderId.Value,
                domainEvent.EstimateId.Value,
                exception);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Failed to send approval email after commit for work order {WorkOrderId} and estimate {EstimateId}.")]
    private static partial void LogApprovalEmailFailure(
        ILogger logger,
        Guid workOrderId,
        Guid estimateId,
        Exception exception);
}
