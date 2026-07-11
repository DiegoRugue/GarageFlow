namespace GarageFlow.Application.WorkOrders.UseCases.AddEstimateInventoryItem;

public sealed record AddEstimateInventoryItemResult(
    Guid EstimateId,
    Guid InventoryItemId,
    string Description,
    int Quantity,
    decimal UnitCost,
    decimal UnitPrice,
    decimal TotalPrice);
