using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleColors.GetVehicleColorById;

public sealed record GetVehicleColorByIdQuery(Guid Id) : IRequest<VehicleColorDto?>;
