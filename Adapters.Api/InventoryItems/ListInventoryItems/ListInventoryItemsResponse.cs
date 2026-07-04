using GarageFlow.Adapters.Api.InventoryItems.GetInventoryItemById;

namespace GarageFlow.Adapters.Api.InventoryItems.ListInventoryItems;

public sealed record ListInventoryItemsResponse(
    IReadOnlyList<InventoryItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
