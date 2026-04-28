namespace GarageFlow.Api.WorkOrders.CreateEstimate;

public sealed record CreateEstimateResponse(
    Guid Id,
    Guid WorkOrderId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt);
