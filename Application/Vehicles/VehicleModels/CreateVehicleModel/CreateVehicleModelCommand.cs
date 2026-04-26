using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleModels.CreateVehicleModel;

public sealed record CreateVehicleModelCommand(
    Guid VehicleBrandId,
    string Name) : IRequest<CreateVehicleModelResult>;
