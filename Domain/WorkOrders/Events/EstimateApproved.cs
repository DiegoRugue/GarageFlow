using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record EstimateApproved(
    WorkOrderId WorkOrderId,
    EstimateId EstimateId,
    EstimateStatus Status,
    DateTime ApprovedAt) : DomainEvent;
