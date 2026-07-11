using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.CreateVehicleModel;

public sealed class CreateVehicleModelHandler(
    IVehicleModelRepository vehicleModelRepository,
    IVehicleBrandRepository vehicleBrandRepository) : IRequestHandler<CreateVehicleModelCommand, CreateVehicleModelResult>
{
    private readonly IVehicleModelRepository _vehicleModelRepository = vehicleModelRepository ?? throw new ArgumentNullException(nameof(vehicleModelRepository));
    private readonly IVehicleBrandRepository _vehicleBrandRepository = vehicleBrandRepository ?? throw new ArgumentNullException(nameof(vehicleBrandRepository));

    public async ValueTask<CreateVehicleModelResult> Handle(CreateVehicleModelCommand request, CancellationToken cancellationToken)
    {
        var vehicleBrandId = VehicleBrandId.From(request.VehicleBrandId);
        var vehicleBrand = await _vehicleBrandRepository.GetByIdAsync(vehicleBrandId, cancellationToken);
        if (vehicleBrand is null)
        {
            throw new NotFoundException($"Vehicle brand with ID '{request.VehicleBrandId}' was not found.");
        }

        var modelName = VehicleModelName.Create(request.Name);

        var exists = await _vehicleModelRepository.ExistsByNameAsync(vehicleBrandId, modelName, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A vehicle model with name '{modelName}' already exists for vehicle brand with ID '{request.VehicleBrandId}'.");
        }

        var vehicleModel = VehicleModel.Create(vehicleBrandId, modelName);

        await _vehicleModelRepository.AddAsync(vehicleModel, cancellationToken);

        return new CreateVehicleModelResult(
            Id: vehicleModel.Id.Value,
            VehicleBrandId: vehicleModel.VehicleBrandId.Value,
            Name: vehicleModel.Name,
            CreatedAt: vehicleModel.CreatedAt);
    }
}
