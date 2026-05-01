namespace GarageFlow.Application.WorkOrders.GetWorkOrderById;

public sealed record WorkOrderEstimateDto(
    Guid Id,
    Guid WorkOrderId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkOrderInventoryLineDto> InventoryLines,
    IReadOnlyList<WorkOrderServiceLineDto> ServiceLines);
