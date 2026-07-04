using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.CreateInventoryItem;

public sealed record CreateInventoryItemCommand(
    string Name,
    string Description,
    int Type,
    decimal Cost,
    decimal Price,
    int StockQuantity) : IRequest<CreateInventoryItemResult>;
