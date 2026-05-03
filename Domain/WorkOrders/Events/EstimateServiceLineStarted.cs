using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record EstimateServiceLineStarted(
    WorkOrderId WorkOrderId,
    EstimateId EstimateId,
    EstimateServiceLineId EstimateServiceLineId,
    ServiceId ServiceId,
    EstimateServiceLineStatus Status,
    DateTime StartedAt) : DomainEvent;
