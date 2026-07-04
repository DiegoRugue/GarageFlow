using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.CreateVehicleBrand;

public sealed class CreateVehicleBrandHandler(
    IVehicleBrandRepository vehicleBrandRepository) : IRequestHandler<CreateVehicleBrandCommand, CreateVehicleBrandResult>
{
    private readonly IVehicleBrandRepository _vehicleBrandRepository = vehicleBrandRepository ?? throw new ArgumentNullException(nameof(vehicleBrandRepository));

    public async ValueTask<CreateVehicleBrandResult> Handle(CreateVehicleBrandCommand request, CancellationToken cancellationToken)
    {
        var brandName = VehicleBrandName.Create(request.Name);

        var exists = await _vehicleBrandRepository.ExistsByNameAsync(brandName, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A vehicle brand with name '{brandName}' already exists.");
        }

        var vehicleBrand = VehicleBrand.Create(brandName);

        await _vehicleBrandRepository.AddAsync(vehicleBrand, cancellationToken);

        return new CreateVehicleBrandResult(
            Id: vehicleBrand.Id.Value,
            Name: vehicleBrand.Name,
            CreatedAt: vehicleBrand.CreatedAt);
    }
}
