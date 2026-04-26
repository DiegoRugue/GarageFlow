using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleBrands.CreateVehicleBrand;

public sealed record CreateVehicleBrandCommand(string Name) : IRequest<CreateVehicleBrandResult>;
