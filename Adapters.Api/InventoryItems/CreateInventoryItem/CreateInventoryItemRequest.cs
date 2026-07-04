namespace GarageFlow.Adapters.Api.InventoryItems.CreateInventoryItem;

public sealed record CreateInventoryItemRequest(
    string Name,
    string Description,
    int Type,
    decimal Cost,
    decimal Price,
    int StockQuantity);
