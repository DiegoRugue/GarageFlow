using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.UpdateVehicleModel;

public sealed record UpdateVehicleModelCommand(
    Guid Id,
    Guid VehicleBrandId,
    string Name) : ICommand<UpdateVehicleModelResult>;
