namespace GarageFlow.Application.WorkOrders;

public sealed record CustomerWorkOrderEstimateDto(
    Guid Id,
    Guid WorkOrderId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CustomerWorkOrderInventoryLineDto> InventoryLines,
    IReadOnlyList<CustomerWorkOrderServiceLineDto> ServiceLines);
