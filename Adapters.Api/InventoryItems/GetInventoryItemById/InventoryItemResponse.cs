namespace GarageFlow.Adapters.Api.InventoryItems.GetInventoryItemById;

public sealed record InventoryItemResponse(
    Guid Id,
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
