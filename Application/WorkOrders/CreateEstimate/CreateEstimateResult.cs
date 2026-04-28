namespace GarageFlow.Application.WorkOrders.CreateEstimate;

public sealed record CreateEstimateResult(Guid Id, Guid WorkOrderId, string Status, decimal TotalAmount, DateTime CreatedAt);
