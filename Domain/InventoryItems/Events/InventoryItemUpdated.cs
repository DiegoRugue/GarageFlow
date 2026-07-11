using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.Domain.InventoryItems.ValueObjects;

namespace GarageFlow.Domain.InventoryItems.Events;

public sealed record InventoryItemUpdated(
    InventoryItemId InventoryItemId,
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price) : DomainEvent;
