namespace GarageFlow.Application.InventoryItems.UseCases.GetInventoryItemById;

public sealed record InventoryItemDto(
    Guid Id,
    string Name,
    string Description,
    int Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
