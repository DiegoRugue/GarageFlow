using GarageFlow.Domain.InventoryItems.Enums;

namespace GarageFlow.Tests.Shared.InventoryItems;

public sealed record UpdateInventoryItemRequest(
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price);
