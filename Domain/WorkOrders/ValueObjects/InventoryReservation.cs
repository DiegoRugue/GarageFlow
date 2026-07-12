using GarageFlow.Domain.InventoryItems.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.ValueObjects;

public sealed record InventoryReservation(
    InventoryItemId InventoryItemId,
    EstimateItemQuantity Quantity);
