namespace GarageFlow.Tests.Shared.InventoryItems;

public sealed record UpdateInventoryItemRequest(
    string Name,
    string Description,
    int Type,
    decimal Cost,
    decimal Price);
