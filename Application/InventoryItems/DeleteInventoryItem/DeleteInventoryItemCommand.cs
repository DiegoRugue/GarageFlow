using Mediator;

namespace GarageFlow.Application.InventoryItems.DeleteInventoryItem;

public sealed record DeleteInventoryItemCommand(Guid Id) : IRequest<Unit>;
