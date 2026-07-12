using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Application.Vehicles.Ports;

public interface IVehicleModelRepository
{
    Task<VehicleModel?> GetByNameAsync(
        VehicleBrandId brandId,
        VehicleModelName name,
        CancellationToken cancellationToken = default);

    Task<VehicleModel?> GetByIdAsync(
        VehicleModelId id,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<VehicleModel> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<VehicleModel> Items, int TotalCount)> ListByVehicleBrandIdAsync(
        VehicleBrandId vehicleBrandId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(VehicleModel vehicleModel, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(
        VehicleBrandId vehicleBrandId,
        VehicleModelName name,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(
        VehicleBrandId vehicleBrandId,
        VehicleModelName name,
        VehicleModelId excludingVehicleModelId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByVehicleBrandIdAsync(
        VehicleBrandId vehicleBrandId,
        CancellationToken cancellationToken = default);

    void Remove(VehicleModel vehicleModel);
}
