using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record ExecutionSchedulingRequested(
    WorkOrderId WorkOrderId,
    EstimateId EstimateId,
    Guid ExecutionId,
    IReadOnlyList<Guid> ServiceLineIds,
    DateTime RequestedAt) : DomainEvent
{
    public const int MaximumServiceLines = 4_096;
}
