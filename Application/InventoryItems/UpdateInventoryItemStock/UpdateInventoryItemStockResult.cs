using GarageFlow.Domain.InventoryItems.Enums;

namespace GarageFlow.Application.InventoryItems.UpdateInventoryItemStock;

public sealed record UpdateInventoryItemStockResult(
    Guid Id,
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
