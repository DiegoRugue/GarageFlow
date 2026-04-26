using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Events;

public sealed record VehicleColorCreated(
    VehicleColorId VehicleColorId,
    string Name,
    DateTime CreatedAt) : DomainEvent;
