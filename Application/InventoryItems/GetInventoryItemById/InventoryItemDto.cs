using GarageFlow.Domain.InventoryItems.Enums;

namespace GarageFlow.Application.InventoryItems.GetInventoryItemById;

public sealed record InventoryItemDto(
    Guid Id,
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
