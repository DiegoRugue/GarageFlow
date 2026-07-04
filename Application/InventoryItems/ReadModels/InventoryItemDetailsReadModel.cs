using GarageFlow.Domain.InventoryItems.Enums;

namespace GarageFlow.Application.InventoryItems.ReadModels;

public sealed record InventoryItemDetailsReadModel(
    Guid Id,
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
