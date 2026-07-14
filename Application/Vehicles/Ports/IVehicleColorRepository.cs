using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Application.Vehicles.Ports;

public interface IVehicleColorRepository
{
    Task<VehicleColor?> GetByNameAsync(
        VehicleColorName name,
        CancellationToken cancellationToken = default);

    Task<VehicleColor?> GetByIdAsync(
        VehicleColorId id,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<VehicleColor> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(VehicleColor vehicleColor, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(
        VehicleColorName name,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(
        VehicleColorName name,
        VehicleColorId excludingVehicleColorId,
        CancellationToken cancellationToken = default);

    void Remove(VehicleColor vehicleColor);
}
