using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Services.ValueObjects;

namespace GarageFlow.Domain.Services.Events;

public sealed record ServiceDeleted(
    ServiceId ServiceId,
    string Description) : DomainEvent;

