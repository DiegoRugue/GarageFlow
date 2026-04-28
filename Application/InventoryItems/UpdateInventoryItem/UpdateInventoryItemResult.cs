using GarageFlow.Domain.InventoryItems.Enums;

namespace GarageFlow.Application.InventoryItems.UpdateInventoryItem;

public sealed record UpdateInventoryItemResult(
    Guid Id,
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
