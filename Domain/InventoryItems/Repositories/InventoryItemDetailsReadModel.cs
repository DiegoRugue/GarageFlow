using GarageFlow.Domain.InventoryItems.Enums;

namespace GarageFlow.Domain.InventoryItems.Repositories;

public sealed record InventoryItemDetailsReadModel(
    Guid Id,
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
