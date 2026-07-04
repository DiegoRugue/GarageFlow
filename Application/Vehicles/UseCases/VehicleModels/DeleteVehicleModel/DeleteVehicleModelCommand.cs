using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.DeleteVehicleModel;

public sealed record DeleteVehicleModelCommand(Guid Id) : IRequest<Unit>;
