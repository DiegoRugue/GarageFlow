using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Events;

public sealed record VehicleDeleted(
    VehicleId VehicleId,
    string LicensePlate) : DomainEvent;
