using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.DeleteVehicleColor;

public sealed record DeleteVehicleColorCommand(Guid Id) : ICommand;
