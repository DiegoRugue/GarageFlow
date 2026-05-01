using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record EstimateInventoryLineAdded(
    WorkOrderId WorkOrderId,
    EstimateId EstimateId,
    EstimateInventoryLineId EstimateInventoryLineId,
    InventoryItemId InventoryItemId,
    int Quantity,
    decimal UnitCost,
    decimal UnitPrice,
    decimal TotalPrice) : DomainEvent;
