using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record EstimateRejected(
    WorkOrderId WorkOrderId,
    EstimateId EstimateId,
    EstimateStatus Status,
    DateTime RejectedAt) : DomainEvent;
