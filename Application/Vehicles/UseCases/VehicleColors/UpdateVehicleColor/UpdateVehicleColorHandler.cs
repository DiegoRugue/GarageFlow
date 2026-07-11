using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.UpdateVehicleColor;

public sealed class UpdateVehicleColorHandler(
    IVehicleColorRepository vehicleColorRepository) : IRequestHandler<UpdateVehicleColorCommand, UpdateVehicleColorResult>
{
    private readonly IVehicleColorRepository _vehicleColorRepository = vehicleColorRepository ?? throw new ArgumentNullException(nameof(vehicleColorRepository));

    public async ValueTask<UpdateVehicleColorResult> Handle(UpdateVehicleColorCommand request, CancellationToken cancellationToken)
    {
        var normalizedName = VehicleColorName.Create(request.Name);

        var vehicleColorId = VehicleColorId.From(request.Id);
        var vehicleColor = await _vehicleColorRepository.GetByIdAsync(vehicleColorId, cancellationToken);
        if (vehicleColor is null)
        {
            throw new NotFoundException($"Vehicle color with ID '{request.Id}' was not found.");
        }

        var exists = await _vehicleColorRepository.ExistsByNameAsync(normalizedName, vehicleColorId, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A vehicle color with name '{normalizedName}' already exists.");
        }

        vehicleColor.Update(normalizedName);

        return new UpdateVehicleColorResult(
            Id: vehicleColor.Id.Value,
            Name: vehicleColor.Name,
            CreatedAt: vehicleColor.CreatedAt);
    }
}
