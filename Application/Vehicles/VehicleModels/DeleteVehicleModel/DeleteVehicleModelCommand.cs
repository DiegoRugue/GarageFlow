using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleModels.DeleteVehicleModel;

public sealed record DeleteVehicleModelCommand(Guid Id) : IRequest<Unit>;
