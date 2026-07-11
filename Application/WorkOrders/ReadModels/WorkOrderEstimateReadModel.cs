namespace GarageFlow.Application.WorkOrders.ReadModels;

public sealed record WorkOrderEstimateReadModel(
    Guid Id,
    Guid WorkOrderId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkOrderInventoryLineReadModel> InventoryLines,
    IReadOnlyList<WorkOrderServiceLineReadModel> ServiceLines);
