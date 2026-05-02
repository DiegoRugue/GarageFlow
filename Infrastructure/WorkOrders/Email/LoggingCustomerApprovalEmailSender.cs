using GarageFlow.Application.WorkOrders.Abstractions;
using Microsoft.Extensions.Logging;

namespace GarageFlow.Infrastructure.WorkOrders.Email;

public sealed class LoggingCustomerApprovalEmailSender(
    ILogger<LoggingCustomerApprovalEmailSender> logger) : ICustomerApprovalEmailSender
{
    private static readonly Action<ILogger, Guid, Guid, Guid, Exception?> LogApprovalEmailSent =
        LoggerMessage.Define<Guid, Guid, Guid>(
            LogLevel.Information,
            new EventId(1, nameof(LogApprovalEmailSent)),
            "Approval email sent to customer {CustomerId} for work order {WorkOrderId} and estimate {EstimateId}.");

    private readonly ILogger<LoggingCustomerApprovalEmailSender> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task SendEstimateWaitingApprovalAsync(
        Guid workOrderId,
        Guid estimateId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        LogApprovalEmailSent(
            _logger,
            customerId,
            workOrderId,
            estimateId,
            null);

        return Task.CompletedTask;
    }
}
