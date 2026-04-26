using Mediator;

namespace GarageFlow.Application.Vehicles.UpdateVehicle;

public sealed record UpdateVehicleCommand(
    Guid Id,
    string Plate,
    int Year,
    Guid CustomerId,
    Guid VehicleModelId,
    Guid VehicleColorId) : IRequest<UpdateVehicleResult>;
