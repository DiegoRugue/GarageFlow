namespace GarageFlow.Application.WorkOrders.GetWorkOrderById;

public sealed record WorkOrderInventoryLineDto(
    Guid Id,
    Guid EstimateId,
    Guid InventoryItemId,
    string Description,
    int Quantity,
    decimal UnitCost,
    decimal UnitPrice,
    decimal TotalPrice);
