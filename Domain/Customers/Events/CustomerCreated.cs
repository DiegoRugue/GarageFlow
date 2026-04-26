using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Customers.ValueObjects;

namespace GarageFlow.Domain.Customers.Events;

public sealed record CustomerCreated(
    CustomerId CustomerId,
    string TaxDocument,
    string FullName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAt) : DomainEvent;
