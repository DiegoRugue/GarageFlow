using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.UpdateVehicleModel;

public sealed class UpdateVehicleModelHandler(
    IVehicleModelRepository vehicleModelRepository,
    IVehicleBrandRepository vehicleBrandRepository,
    IVehicleRepository vehicleRepository) : IRequestHandler<UpdateVehicleModelCommand, UpdateVehicleModelResult>
{
    private readonly IVehicleModelRepository _vehicleModelRepository = vehicleModelRepository ?? throw new ArgumentNullException(nameof(vehicleModelRepository));
    private readonly IVehicleBrandRepository _vehicleBrandRepository = vehicleBrandRepository ?? throw new ArgumentNullException(nameof(vehicleBrandRepository));
    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));

    public async ValueTask<UpdateVehicleModelResult> Handle(UpdateVehicleModelCommand request, CancellationToken cancellationToken)
    {
        var vehicleModelId = VehicleModelId.From(request.Id);
        var vehicleModel = await _vehicleModelRepository.GetByIdAsync(vehicleModelId, cancellationToken);
        if (vehicleModel is null)
        {
            throw new NotFoundException($"Vehicle model with ID '{request.Id}' was not found.");
        }

        var vehicleBrandId = VehicleBrandId.From(request.VehicleBrandId);
        var vehicleBrand = await _vehicleBrandRepository.GetByIdAsync(vehicleBrandId, cancellationToken);
        if (vehicleBrand is null)
        {
            throw new NotFoundException($"Vehicle brand with ID '{request.VehicleBrandId}' was not found.");
        }

        var isReassigningBrand = vehicleModel.VehicleBrandId != vehicleBrandId;
        if (isReassigningBrand)
        {
            var hasRelatedVehicles = await _vehicleRepository.ExistsByVehicleModelIdAsync(vehicleModel.Id, cancellationToken);
            if (hasRelatedVehicles)
            {
                throw new BusinessRuleViolationException($"Vehicle model with ID '{request.Id}' cannot change vehicle brand because it has related vehicles.");
            }
        }

        var normalizedName = VehicleModelName.Create(request.Name);

        var exists = await _vehicleModelRepository.ExistsByNameAsync(vehicleBrandId, normalizedName, vehicleModelId, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A vehicle model with name '{normalizedName}' already exists for vehicle brand with ID '{request.VehicleBrandId}'.");
        }

        vehicleModel.Update(vehicleBrandId, normalizedName);

        return new UpdateVehicleModelResult(
            Id: vehicleModel.Id.Value,
            VehicleBrandId: vehicleModel.VehicleBrandId.Value,
            Name: vehicleModel.Name,
            CreatedAt: vehicleModel.CreatedAt);
    }
}
