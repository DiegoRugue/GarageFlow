using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.UpdateVehicleBrand;

public sealed record UpdateVehicleBrandCommand(
    Guid Id,
    string Name) : ICommand<UpdateVehicleBrandResult>;
