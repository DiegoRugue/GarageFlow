using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record ExecutionChargeRequested(
    WorkOrderId WorkOrderId,
    EstimateId EstimateId,
    Guid ChargeId,
    Guid MessageId,
    decimal Amount,
    DateTime RequestedAt) : DomainEvent;
