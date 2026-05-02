using GarageFlow.Application.WorkOrders.Abstractions;
using Microsoft.Extensions.Logging;

namespace GarageFlow.Infrastructure.WorkOrders.Email;

public sealed class LoggingCustomerApprovalEmailSender(
    ILogger<LoggingCustomerApprovalEmailSender> logger) : ICustomerApprovalEmailSender
{
    private readonly ILogger<LoggingCustomerApprovalEmailSender> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task SendEstimateWaitingApprovalAsync(
        Guid workOrderId,
        Guid estimateId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Approval email sent to customer {CustomerId} for work order {WorkOrderId} and estimate {EstimateId}.",
            customerId,
            workOrderId,
            estimateId);

        return Task.CompletedTask;
    }
}
