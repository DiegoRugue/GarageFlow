using GarageFlow.Domain.InventoryItems.Enums;

namespace GarageFlow.Application.InventoryItems.UseCases.CreateInventoryItem;

public sealed record CreateInventoryItemResult(
    Guid Id,
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
