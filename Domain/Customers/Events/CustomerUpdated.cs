using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Customers.ValueObjects;

namespace GarageFlow.Domain.Customers.Events;

public sealed record CustomerUpdated(
    CustomerId CustomerId,
    string FullName,
    string Email,
    string PhoneNumber) : DomainEvent;
