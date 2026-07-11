using GarageFlow.Application.InventoryItems.UseCases.GetInventoryItemById;

namespace GarageFlow.Application.InventoryItems.UseCases.ListInventoryItems;

public sealed record ListInventoryItemsResult(
    IReadOnlyList<InventoryItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
