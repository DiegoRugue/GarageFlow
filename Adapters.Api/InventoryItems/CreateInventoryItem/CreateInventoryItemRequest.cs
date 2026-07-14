namespace GarageFlow.Adapters.Api.InventoryItems.CreateInventoryItem;

public sealed record CreateInventoryItemRequest(
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity);
