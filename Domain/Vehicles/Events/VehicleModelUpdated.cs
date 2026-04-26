using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Events;

public sealed record VehicleModelUpdated(
    VehicleModelId VehicleModelId,
    VehicleBrandId VehicleBrandId,
    string Name) : DomainEvent;
