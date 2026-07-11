using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.CreateVehicleBrand;

public sealed record CreateVehicleBrandCommand(string Name) : ICommand<CreateVehicleBrandResult>;
