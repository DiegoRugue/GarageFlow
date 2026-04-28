using GarageFlow.Domain.InventoryItems.Enums;

namespace GarageFlow.Tests.Shared.InventoryItems;

public sealed record CreateInventoryItemRequest(
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price,
    int StockQuantity);
