namespace GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItemStock;

public sealed record UpdateInventoryItemStockResult(
    Guid Id,
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
