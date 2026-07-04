using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.InventoryItems.ValueObjects;

namespace GarageFlow.Domain.InventoryItems.Events;

public sealed record InventoryItemDeleted(
    InventoryItemId InventoryItemId,
    string Name) : DomainEvent;
