using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.CreateVehicleModel;

public sealed record CreateVehicleModelCommand(
    Guid VehicleBrandId,
    string Name) : IRequest<CreateVehicleModelResult>;
