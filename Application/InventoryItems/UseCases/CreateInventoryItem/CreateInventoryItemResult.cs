namespace GarageFlow.Application.InventoryItems.UseCases.CreateInventoryItem;

public sealed record CreateInventoryItemResult(
    Guid Id,
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
