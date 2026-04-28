using GarageFlow.Domain.InventoryItems.Enums;

namespace GarageFlow.Api.InventoryItems.CreateInventoryItem;

public sealed record CreateInventoryItemRequest(
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price,
    int StockQuantity);
