using GarageFlow.Application.InventoryItems.GetInventoryItemById;

namespace GarageFlow.Application.InventoryItems.ListInventoryItems;

public sealed record ListInventoryItemsResult(
    IReadOnlyList<InventoryItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
