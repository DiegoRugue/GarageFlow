using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.UpdateVehicleModel;

public sealed record UpdateVehicleModelCommand(
    Guid Id,
    Guid VehicleBrandId,
    string Name) : IRequest<UpdateVehicleModelResult>;
