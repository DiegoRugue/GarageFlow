using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.GetVehicleBrandById;

public sealed record GetVehicleBrandByIdQuery(Guid Id) : IRequest<VehicleBrandDto>;
