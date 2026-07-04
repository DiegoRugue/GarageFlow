using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.ListInventoryItems;

public sealed record ListInventoryItemsQuery(
    int Page = 1,
    int PageSize = 20) : IRequest<ListInventoryItemsResult>;
