using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.InventoryItems.ValueObjects;

namespace GarageFlow.Domain.InventoryItems.Events;

public sealed record InventoryItemStockUpdated(
    InventoryItemId InventoryItemId,
    int PreviousStockQuantity,
    int NewStockQuantity) : DomainEvent;
