namespace GarageFlow.Application.InventoryItems.UseCases.CreateInventoryItem;

public sealed record CreateInventoryItemResult(
    Guid Id,
    string Name,
    string Description,
    int Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
