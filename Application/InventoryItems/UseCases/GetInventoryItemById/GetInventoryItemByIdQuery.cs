using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.GetInventoryItemById;

public sealed record GetInventoryItemByIdQuery(Guid Id) : IRequest<InventoryItemDto>;
