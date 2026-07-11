namespace GarageFlow.Adapters.Api.WorkOrders.Responses;

public sealed record WorkOrderEstimateResponse(
    Guid Id,
    Guid WorkOrderId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkOrderInventoryLineResponse> InventoryLines,
    IReadOnlyList<WorkOrderServiceLineResponse> ServiceLines);
