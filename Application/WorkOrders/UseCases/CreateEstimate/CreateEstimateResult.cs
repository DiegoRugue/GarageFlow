namespace GarageFlow.Application.WorkOrders.UseCases.CreateEstimate;

public sealed record CreateEstimateResult(Guid Id, Guid WorkOrderId, string Status, decimal TotalAmount, DateTime CreatedAt);
