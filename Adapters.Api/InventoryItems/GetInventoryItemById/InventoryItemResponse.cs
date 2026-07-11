namespace GarageFlow.Adapters.Api.InventoryItems.GetInventoryItemById;

public sealed record InventoryItemResponse(
    Guid Id,
    string Name,
    string Description,
    int Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
