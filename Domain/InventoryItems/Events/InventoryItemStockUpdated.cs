using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.InventoryItems.ValueObjects;

namespace GarageFlow.Domain.InventoryItems.Events;

public sealed record InventoryItemStockUpdated(
    InventoryItemId InventoryItemId,
    int PreviousStockQuantity,
    int NewStockQuantity) : DomainEvent;
