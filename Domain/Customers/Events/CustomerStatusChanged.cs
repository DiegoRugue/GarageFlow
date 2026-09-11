using GarageFlow.Domain.Customers.Enums;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Domain.Customers.Events;

public sealed record CustomerStatusChanged(
    CustomerId CustomerId,
    CustomerStatus PreviousStatus,
    CustomerStatus Status) : DomainEvent;
