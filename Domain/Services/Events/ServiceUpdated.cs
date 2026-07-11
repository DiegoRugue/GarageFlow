using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.Services.ValueObjects;

namespace GarageFlow.Domain.Services.Events;

public sealed record ServiceUpdated(
    ServiceId ServiceId,
    string Description,
    decimal Price) : DomainEvent;

