namespace GarageFlow.Adapters.Api.InventoryItems.UpdateInventoryItem;

public sealed record UpdateInventoryItemRequest(
    string Name,
    string Description,
    int Type,
    decimal Cost,
    decimal Price);
