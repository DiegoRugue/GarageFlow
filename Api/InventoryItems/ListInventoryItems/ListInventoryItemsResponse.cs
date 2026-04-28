using GarageFlow.Api.InventoryItems.GetInventoryItemById;

namespace GarageFlow.Api.InventoryItems.ListInventoryItems;

public sealed record ListInventoryItemsResponse(
    IReadOnlyList<InventoryItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
