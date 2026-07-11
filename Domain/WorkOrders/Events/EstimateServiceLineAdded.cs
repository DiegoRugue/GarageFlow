using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Events;

public sealed record EstimateServiceLineAdded(
    WorkOrderId WorkOrderId,
    EstimateId EstimateId,
    EstimateServiceLineId EstimateServiceLineId,
    ServiceId ServiceId,
    decimal UnitPrice,
    decimal TotalPrice) : DomainEvent;
