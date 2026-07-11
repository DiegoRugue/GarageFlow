using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record EstimateWaitingApprovalRequested(
    WorkOrderId WorkOrderId,
    EstimateId EstimateId,
    CustomerId CustomerId,
    DateTime RequestedAt) : DomainEvent;
