namespace GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;

public sealed record WorkOrderEstimateResponse(
    Guid Id,
    Guid WorkOrderId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkOrderInventoryLineResponse> InventoryLines,
    IReadOnlyList<WorkOrderServiceLineResponse> ServiceLines);
