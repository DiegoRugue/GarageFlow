namespace GarageFlow.Adapters.Api.InventoryItems.CreateInventoryItem;

public sealed record CreateInventoryItemResponse(
    Guid Id,
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
