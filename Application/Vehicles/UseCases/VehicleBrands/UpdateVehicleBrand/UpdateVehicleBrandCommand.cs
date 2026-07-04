using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.UpdateVehicleBrand;

public sealed record UpdateVehicleBrandCommand(
    Guid Id,
    string Name) : IRequest<UpdateVehicleBrandResult>;
