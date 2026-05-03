namespace GarageFlow.Application.WorkOrders.Abstractions;

public interface ICustomerApprovalEmailSender
{
    Task SendEstimateWaitingApprovalAsync(
        Guid workOrderId,
        Guid estimateId,
        Guid customerId,
        CancellationToken cancellationToken);
}
