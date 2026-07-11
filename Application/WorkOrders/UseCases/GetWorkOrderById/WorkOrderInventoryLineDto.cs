namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderById;

public sealed record WorkOrderInventoryLineDto(
    Guid Id,
    Guid EstimateId,
    Guid InventoryItemId,
    string Description,
    int Quantity,
    decimal UnitCost,
    decimal UnitPrice,
    decimal TotalPrice);
