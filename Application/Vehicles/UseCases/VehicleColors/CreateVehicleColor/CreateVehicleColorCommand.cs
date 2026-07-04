using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.CreateVehicleColor;

public sealed record CreateVehicleColorCommand(string Name) : IRequest<CreateVehicleColorResult>;
