using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Events;

public sealed record VehicleBrandUpdated(
    VehicleBrandId VehicleBrandId,
    string Name) : DomainEvent;
