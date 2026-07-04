using GarageFlow.Domain.InventoryItems.Enums;

namespace GarageFlow.Adapters.Api.InventoryItems.GetInventoryItemById;

public sealed record InventoryItemResponse(
    Guid Id,
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
