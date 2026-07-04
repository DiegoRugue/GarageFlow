using GarageFlow.Domain.InventoryItems.Enums;
using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItem;

public sealed record UpdateInventoryItemCommand(
    Guid Id,
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price) : IRequest<UpdateInventoryItemResult>;
