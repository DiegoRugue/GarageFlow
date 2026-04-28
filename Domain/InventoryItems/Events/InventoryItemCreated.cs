using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.Domain.InventoryItems.ValueObjects;

namespace GarageFlow.Domain.InventoryItems.Events;

public sealed record InventoryItemCreated(
    InventoryItemId InventoryItemId,
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt) : DomainEvent;
