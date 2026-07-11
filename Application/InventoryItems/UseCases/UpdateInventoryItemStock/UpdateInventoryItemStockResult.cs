namespace GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItemStock;

public sealed record UpdateInventoryItemStockResult(
    Guid Id,
    string Name,
    string Description,
    int Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
