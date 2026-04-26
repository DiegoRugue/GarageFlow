using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleColors.CreateVehicleColor;

public sealed record CreateVehicleColorCommand(string Name) : IRequest<CreateVehicleColorResult>;
