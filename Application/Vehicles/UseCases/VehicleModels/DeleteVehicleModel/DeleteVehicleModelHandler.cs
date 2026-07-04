using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.DeleteVehicleModel;

public sealed class DeleteVehicleModelHandler(
    IVehicleModelRepository vehicleModelRepository,
    IVehicleRepository vehicleRepository) : IRequestHandler<DeleteVehicleModelCommand, Unit>
{
    private readonly IVehicleModelRepository _vehicleModelRepository = vehicleModelRepository ?? throw new ArgumentNullException(nameof(vehicleModelRepository));
    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));

    public async ValueTask<Unit> Handle(DeleteVehicleModelCommand request, CancellationToken cancellationToken)
    {
        var vehicleModelId = VehicleModelId.From(request.Id);
        var vehicleModel = await _vehicleModelRepository.GetByIdAsync(vehicleModelId, cancellationToken);
        if (vehicleModel is null)
        {
            throw new NotFoundException($"Vehicle model with ID '{request.Id}' was not found.");
        }

        var hasVehicles = await _vehicleRepository.ExistsByVehicleModelIdAsync(vehicleModelId, cancellationToken);
        if (hasVehicles)
        {
            throw new BusinessRuleViolationException($"Vehicle model with ID '{request.Id}' cannot be deleted because it has related vehicles.");
        }

        vehicleModel.Delete();
        _vehicleModelRepository.Remove(vehicleModel);

        return Unit.Value;
    }
}
