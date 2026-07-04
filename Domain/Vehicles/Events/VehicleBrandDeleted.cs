using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Events;

public sealed record VehicleBrandDeleted(
    VehicleBrandId VehicleBrandId,
    string Name) : DomainEvent;
