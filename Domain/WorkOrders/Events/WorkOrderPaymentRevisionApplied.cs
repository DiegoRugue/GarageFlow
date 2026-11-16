using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record WorkOrderPaymentRevisionApplied(
    WorkOrderId WorkOrderId,
    EstimateId EstimateId,
    Guid ChargeId,
    long Revision,
    WorkOrderPaymentState State) : DomainEvent;
