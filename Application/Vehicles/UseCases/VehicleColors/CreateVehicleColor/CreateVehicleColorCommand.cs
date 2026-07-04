using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.CreateVehicleColor;

public sealed record CreateVehicleColorCommand(string Name) : ICommand<CreateVehicleColorResult>;
