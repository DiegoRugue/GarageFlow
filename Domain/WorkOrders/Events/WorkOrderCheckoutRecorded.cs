using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record WorkOrderCheckoutRecorded(
    WorkOrderId WorkOrderId,
    EstimateId EstimateId,
    Guid ChargeId) : DomainEvent;
