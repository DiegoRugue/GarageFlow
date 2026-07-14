using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItem;

public sealed record UpdateInventoryItemCommand(
    Guid Id,
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price) : ICommand<UpdateInventoryItemResult>;
