namespace GarageFlow.Adapters.Api.InventoryItems.UpdateInventoryItem;

public sealed record UpdateInventoryItemRequest(
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price);
