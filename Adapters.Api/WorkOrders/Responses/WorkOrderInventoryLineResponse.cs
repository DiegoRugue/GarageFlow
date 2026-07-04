namespace GarageFlow.Adapters.Api.WorkOrders.Responses;

public sealed record WorkOrderInventoryLineResponse(
    Guid Id,
    Guid EstimateId,
    Guid InventoryItemId,
    string Description,
    int Quantity,
    decimal UnitCost,
    decimal UnitPrice,
    decimal TotalPrice);
