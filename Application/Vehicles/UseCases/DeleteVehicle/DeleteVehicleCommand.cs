using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Vehicles.UseCases.DeleteVehicle;

public sealed record DeleteVehicleCommand(Guid Id) : ICommand;
