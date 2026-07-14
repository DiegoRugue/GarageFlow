namespace GarageFlow.Tests.Shared.InventoryItems;

public sealed record CreateInventoryItemRequest(
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity);
