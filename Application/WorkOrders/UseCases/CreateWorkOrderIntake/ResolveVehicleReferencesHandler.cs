using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;

public sealed class ResolveVehicleReferencesHandler(
    IVehicleBrandRepository brandRepository,
    IVehicleModelRepository modelRepository,
    IVehicleColorRepository colorRepository)
{
    private readonly IVehicleBrandRepository _brandRepository = brandRepository
        ?? throw new ArgumentNullException(nameof(brandRepository));
    private readonly IVehicleModelRepository _modelRepository = modelRepository
        ?? throw new ArgumentNullException(nameof(modelRepository));
    private readonly IVehicleColorRepository _colorRepository = colorRepository
        ?? throw new ArgumentNullException(nameof(colorRepository));

    public async Task<(VehicleBrand Brand, VehicleModel Model, VehicleColor Color)> ResolveAsync(
        VehicleBrandName brandName,
        VehicleModelName modelName,
        VehicleColorName colorName,
        CancellationToken cancellationToken)
    {
        var brand = await _brandRepository.GetByNameAsync(brandName, cancellationToken);
        if (brand is null)
        {
            brand = VehicleBrand.Create(brandName.Value);
            await _brandRepository.AddAsync(brand, cancellationToken);
        }

        var model = await _modelRepository.GetByNameAsync(brand.Id, modelName, cancellationToken);
        if (model is null)
        {
            model = VehicleModel.Create(brand.Id, modelName.Value);
            await _modelRepository.AddAsync(model, cancellationToken);
        }

        var color = await _colorRepository.GetByNameAsync(colorName, cancellationToken);
        if (color is null)
        {
            color = VehicleColor.Create(colorName.Value);
            await _colorRepository.AddAsync(color, cancellationToken);
        }

        return (brand, model, color);
    }
}
