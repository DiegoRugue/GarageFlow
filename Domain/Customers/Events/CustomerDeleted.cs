using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.Customers.ValueObjects;

namespace GarageFlow.Domain.Customers.Events;

public sealed record CustomerDeleted(
    CustomerId CustomerId,
    string TaxDocument) : DomainEvent;
