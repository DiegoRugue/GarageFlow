using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.CreateVehicleBrand;

public sealed record CreateVehicleBrandCommand(string Name) : IRequest<CreateVehicleBrandResult>;
