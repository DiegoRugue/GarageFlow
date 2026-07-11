using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.GetVehicleColorById;

public sealed record GetVehicleColorByIdQuery(Guid Id) : IRequest<VehicleColorDto>;
