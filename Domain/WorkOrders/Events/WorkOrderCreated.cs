using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record WorkOrderCreated(
    WorkOrderId WorkOrderId,
    CustomerId CustomerId,
    VehicleId VehicleId,
    WorkOrderStatus Status,
    DateTime CreatedAt) : DomainEvent;
