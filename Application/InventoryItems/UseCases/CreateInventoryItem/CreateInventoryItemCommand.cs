using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.InventoryItems.UseCases.CreateInventoryItem;

public sealed record CreateInventoryItemCommand(
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity) : ICommand<CreateInventoryItemResult>;
