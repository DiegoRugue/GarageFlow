using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.DeleteVehicleBrand;

public sealed record DeleteVehicleBrandCommand(Guid Id) : ICommand;
