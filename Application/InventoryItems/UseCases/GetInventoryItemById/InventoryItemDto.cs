namespace GarageFlow.Application.InventoryItems.UseCases.GetInventoryItemById;

public sealed record InventoryItemDto(
    Guid Id,
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
