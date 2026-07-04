using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.DeleteInventoryItem;

public sealed record DeleteInventoryItemCommand(Guid Id) : IRequest<Unit>;
