using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record ExecutionLifecycleRequested(
    WorkOrderId WorkOrderId,
    EstimateId EstimateId,
    Guid ExecutionId,
    Guid MessageId,
    bool Complete,
    DateTime RequestedAt) : DomainEvent;
