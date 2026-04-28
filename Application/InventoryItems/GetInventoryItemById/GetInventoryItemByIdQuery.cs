using Mediator;

namespace GarageFlow.Application.InventoryItems.GetInventoryItemById;

public sealed record GetInventoryItemByIdQuery(Guid Id) : IRequest<InventoryItemDto?>;
