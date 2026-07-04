namespace GarageFlow.Application.InventoryItems.ReadModels;

public sealed record InventoryItemDetailsReadModel(
    Guid Id,
    string Name,
    string Description,
    int Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
