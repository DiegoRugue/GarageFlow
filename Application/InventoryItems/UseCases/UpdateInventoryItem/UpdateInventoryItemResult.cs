namespace GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItem;

public sealed record UpdateInventoryItemResult(
    Guid Id,
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
