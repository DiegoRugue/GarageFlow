using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.Services.ValueObjects;

namespace GarageFlow.Domain.Services.Events;

public sealed record ServiceCreated(
    ServiceId ServiceId,
    string Description,
    decimal Price,
    DateTime CreatedAt) : DomainEvent;
