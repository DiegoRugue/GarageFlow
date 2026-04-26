using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleBrands.UpdateVehicleBrand;

public sealed record UpdateVehicleBrandCommand(
    Guid Id,
    string Name) : IRequest<UpdateVehicleBrandResult>;
