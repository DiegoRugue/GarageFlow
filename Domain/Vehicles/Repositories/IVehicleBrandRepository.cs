using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Repositories;

public interface IVehicleBrandRepository
{
    Task<VehicleBrand?> GetByIdAsync(
        VehicleBrandId id,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<VehicleBrand> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(VehicleBrand vehicleBrand, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(
        string name,
        VehicleBrandId excludingVehicleBrandId,
        CancellationToken cancellationToken = default);

    void Remove(VehicleBrand vehicleBrand);
}
