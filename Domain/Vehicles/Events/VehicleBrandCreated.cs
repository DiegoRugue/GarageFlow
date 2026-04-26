using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Events;

public sealed record VehicleBrandCreated(
    VehicleBrandId VehicleBrandId,
    string Name,
    DateTime CreatedAt) : DomainEvent;
