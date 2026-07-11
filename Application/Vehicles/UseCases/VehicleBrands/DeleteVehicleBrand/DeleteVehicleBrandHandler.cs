using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.DeleteVehicleBrand;

public sealed class DeleteVehicleBrandHandler(
    IVehicleBrandRepository vehicleBrandRepository,
    IVehicleModelRepository vehicleModelRepository) : IRequestHandler<DeleteVehicleBrandCommand, Unit>
{
    private readonly IVehicleBrandRepository _vehicleBrandRepository = vehicleBrandRepository ?? throw new ArgumentNullException(nameof(vehicleBrandRepository));
    private readonly IVehicleModelRepository _vehicleModelRepository = vehicleModelRepository ?? throw new ArgumentNullException(nameof(vehicleModelRepository));

    public async ValueTask<Unit> Handle(DeleteVehicleBrandCommand request, CancellationToken cancellationToken)
    {
        var vehicleBrandId = VehicleBrandId.From(request.Id);
        var vehicleBrand = await _vehicleBrandRepository.GetByIdAsync(vehicleBrandId, cancellationToken);
        if (vehicleBrand is null)
        {
            throw new NotFoundException($"Vehicle brand with ID '{request.Id}' was not found.");
        }

        var hasModels = await _vehicleModelRepository.ExistsByVehicleBrandIdAsync(vehicleBrandId, cancellationToken);
        if (hasModels)
        {
            throw new BusinessRuleViolationException($"Vehicle brand with ID '{request.Id}' cannot be deleted because it has related vehicle models.");
        }

        vehicleBrand.Delete();
        _vehicleBrandRepository.Remove(vehicleBrand);

        return Unit.Value;
    }
}
