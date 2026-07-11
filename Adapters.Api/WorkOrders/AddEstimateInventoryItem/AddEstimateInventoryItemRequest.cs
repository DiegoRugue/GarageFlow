namespace GarageFlow.Adapters.Api.WorkOrders.AddEstimateInventoryItem;

public sealed record AddEstimateInventoryItemRequest(Guid InventoryItemId, int Quantity);
