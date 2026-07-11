namespace GarageFlow.Adapters.Api.WorkOrders.AddEstimateInventoryItem;

public sealed record AddEstimateInventoryItemResponse(
    Guid EstimateId,
    Guid InventoryItemId,
    string Description,
    int Quantity,
    decimal UnitCost,
    decimal UnitPrice,
    decimal TotalPrice);
