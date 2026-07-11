using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.CreateVehicleColor;

public sealed class CreateVehicleColorHandler(
    IVehicleColorRepository vehicleColorRepository) : IRequestHandler<CreateVehicleColorCommand, CreateVehicleColorResult>
{
    private readonly IVehicleColorRepository _vehicleColorRepository = vehicleColorRepository ?? throw new ArgumentNullException(nameof(vehicleColorRepository));

    public async ValueTask<CreateVehicleColorResult> Handle(CreateVehicleColorCommand request, CancellationToken cancellationToken)
    {
        var colorName = VehicleColorName.Create(request.Name);

        var exists = await _vehicleColorRepository.ExistsByNameAsync(colorName, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A vehicle color with name '{colorName}' already exists.");
        }

        var vehicleColor = VehicleColor.Create(colorName);

        await _vehicleColorRepository.AddAsync(vehicleColor, cancellationToken);

        return new CreateVehicleColorResult(
            Id: vehicleColor.Id.Value,
            Name: vehicleColor.Name,
            CreatedAt: vehicleColor.CreatedAt);
    }
}
