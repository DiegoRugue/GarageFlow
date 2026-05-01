using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record WorkOrderStatusChanged(
    WorkOrderId WorkOrderId,
    WorkOrderStatus PreviousStatus,
    WorkOrderStatus NewStatus,
    DateTime UpdatedAt) : DomainEvent;
