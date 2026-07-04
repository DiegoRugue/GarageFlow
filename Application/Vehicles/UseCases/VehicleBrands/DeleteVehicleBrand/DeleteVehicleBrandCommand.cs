using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.DeleteVehicleBrand;

public sealed record DeleteVehicleBrandCommand(Guid Id) : IRequest<Unit>;
