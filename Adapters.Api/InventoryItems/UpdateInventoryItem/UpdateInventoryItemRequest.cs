using GarageFlow.Domain.InventoryItems.Enums;

namespace GarageFlow.Adapters.Api.InventoryItems.UpdateInventoryItem;

public sealed record UpdateInventoryItemRequest(
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price);
