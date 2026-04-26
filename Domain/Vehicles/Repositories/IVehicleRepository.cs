using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Repositories;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(VehicleId id, CancellationToken cancellationToken = default);

    Task<VehicleDetailsReadModel?> GetDetailsByIdAsync(
        VehicleId id,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<VehicleDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CustomerId? customerId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleDetailsReadModel>> ListDetailsByCustomerIdAsync(
        CustomerId customerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default);

    Task<bool> ExistsByLicensePlateAsync(
        LicensePlate licensePlate,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByLicensePlateAsync(
        LicensePlate licensePlate,
        VehicleId excludingVehicleId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByVehicleBrandIdAsync(
        VehicleBrandId vehicleBrandId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByVehicleModelIdAsync(
        VehicleModelId vehicleModelId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByVehicleColorIdAsync(
        VehicleColorId vehicleColorId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByCustomerIdAsync(
        CustomerId customerId,
        CancellationToken cancellationToken = default);

    void Remove(Vehicle vehicle);
}
