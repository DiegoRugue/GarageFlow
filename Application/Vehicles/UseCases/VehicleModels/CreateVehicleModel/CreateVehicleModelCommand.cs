using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.CreateVehicleModel;

public sealed record CreateVehicleModelCommand(
    Guid VehicleBrandId,
    string Name) : ICommand<CreateVehicleModelResult>;
