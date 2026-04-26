using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Events;

public sealed record VehicleColorDeleted(
    VehicleColorId VehicleColorId,
    string Name) : DomainEvent;
