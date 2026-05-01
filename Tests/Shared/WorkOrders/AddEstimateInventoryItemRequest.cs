namespace GarageFlow.Tests.Shared.WorkOrders;

public sealed record AddEstimateInventoryItemRequest(Guid InventoryItemId, int Quantity);
