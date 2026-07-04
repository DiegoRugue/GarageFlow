using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.DeleteVehicleModel;

public sealed record DeleteVehicleModelCommand(Guid Id) : ICommand;
