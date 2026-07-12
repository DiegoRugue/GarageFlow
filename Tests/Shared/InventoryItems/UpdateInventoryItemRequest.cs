namespace GarageFlow.Tests.Shared.InventoryItems;

public sealed record UpdateInventoryItemRequest(
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price);
