using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Tests.Unit.Vehicles;

public class VehicleRepositoryContractsCompilationTests
{
    [Fact]
    public void Task2_VehicleRepositoryContracts_ShouldCompileAgainstExpectedSignatures()
    {
        Signature<Func<IVehicleRepository, VehicleId, CancellationToken, Task<Vehicle?>>>(
            (repository, id, cancellationToken) => repository.GetByIdAsync(id, cancellationToken));
        Signature<Func<IVehicleRepository, VehicleId, CancellationToken, Task<VehicleDetailsReadModel?>>>(
            (repository, id, cancellationToken) => repository.GetDetailsByIdAsync(id, cancellationToken));
        Signature<Func<IVehicleRepository, int, int, CancellationToken, Task<(IReadOnlyList<Vehicle> Items, int TotalCount)>>>(
            (repository, page, pageSize, cancellationToken) => repository.ListAsync(page, pageSize, cancellationToken));
        Signature<Func<IVehicleRepository, int, int, CustomerId?, CancellationToken, Task<(IReadOnlyList<VehicleDetailsReadModel> Items, int TotalCount)>>>(
            (repository, page, pageSize, customerId, cancellationToken) => repository.ListDetailsAsync(page, pageSize, customerId, cancellationToken));
        Signature<Func<IVehicleRepository, CustomerId, CancellationToken, Task<IReadOnlyList<VehicleDetailsReadModel>>>>(
            (repository, customerId, cancellationToken) => repository.ListDetailsByCustomerIdAsync(customerId, cancellationToken));
        Signature<Func<IVehicleRepository, Vehicle, CancellationToken, Task>>(
            (repository, vehicle, cancellationToken) => repository.AddAsync(vehicle, cancellationToken));
        Signature<Func<IVehicleRepository, LicensePlate, CancellationToken, Task<bool>>>(
            (repository, licensePlate, cancellationToken) => repository.ExistsByLicensePlateAsync(licensePlate, cancellationToken));
        Signature<Func<IVehicleRepository, LicensePlate, VehicleId, CancellationToken, Task<bool>>>(
            (repository, licensePlate, excludingVehicleId, cancellationToken) => repository.ExistsByLicensePlateAsync(licensePlate, excludingVehicleId, cancellationToken));
        Signature<Func<IVehicleRepository, VehicleBrandId, CancellationToken, Task<bool>>>(
            (repository, vehicleBrandId, cancellationToken) => repository.ExistsByVehicleBrandIdAsync(vehicleBrandId, cancellationToken));
        Signature<Func<IVehicleRepository, VehicleModelId, CancellationToken, Task<bool>>>(
            (repository, vehicleModelId, cancellationToken) => repository.ExistsByVehicleModelIdAsync(vehicleModelId, cancellationToken));
        Signature<Func<IVehicleRepository, VehicleColorId, CancellationToken, Task<bool>>>(
            (repository, vehicleColorId, cancellationToken) => repository.ExistsByVehicleColorIdAsync(vehicleColorId, cancellationToken));
        Signature<Func<IVehicleRepository, CustomerId, CancellationToken, Task<bool>>>(
            (repository, customerId, cancellationToken) => repository.ExistsByCustomerIdAsync(customerId, cancellationToken));
        Signature<Action<IVehicleRepository, Vehicle>>(
            (repository, vehicle) => repository.Remove(vehicle));

        Signature<Func<IVehicleBrandRepository, VehicleBrandId, CancellationToken, Task<VehicleBrand?>>>(
            (repository, id, cancellationToken) => repository.GetByIdAsync(id, cancellationToken));
        Signature<Func<IVehicleBrandRepository, int, int, CancellationToken, Task<(IReadOnlyList<VehicleBrand> Items, int TotalCount)>>>(
            (repository, page, pageSize, cancellationToken) => repository.ListAsync(page, pageSize, cancellationToken));
        Signature<Func<IVehicleBrandRepository, VehicleBrand, CancellationToken, Task>>(
            (repository, vehicleBrand, cancellationToken) => repository.AddAsync(vehicleBrand, cancellationToken));
        Signature<Func<IVehicleBrandRepository, VehicleBrandName, CancellationToken, Task<bool>>>(
            (repository, name, cancellationToken) => repository.ExistsByNameAsync(name, cancellationToken));
        Signature<Func<IVehicleBrandRepository, VehicleBrandName, VehicleBrandId, CancellationToken, Task<bool>>>(
            (repository, name, excludingVehicleBrandId, cancellationToken) => repository.ExistsByNameAsync(name, excludingVehicleBrandId, cancellationToken));
        Signature<Action<IVehicleBrandRepository, VehicleBrand>>(
            (repository, vehicleBrand) => repository.Remove(vehicleBrand));

        Signature<Func<IVehicleModelRepository, VehicleModelId, CancellationToken, Task<VehicleModel?>>>(
            (repository, id, cancellationToken) => repository.GetByIdAsync(id, cancellationToken));
        Signature<Func<IVehicleModelRepository, int, int, CancellationToken, Task<(IReadOnlyList<VehicleModel> Items, int TotalCount)>>>(
            (repository, page, pageSize, cancellationToken) => repository.ListAsync(page, pageSize, cancellationToken));
        Signature<Func<IVehicleModelRepository, VehicleBrandId, int, int, CancellationToken, Task<(IReadOnlyList<VehicleModel> Items, int TotalCount)>>>(
            (repository, vehicleBrandId, page, pageSize, cancellationToken) => repository.ListByVehicleBrandIdAsync(vehicleBrandId, page, pageSize, cancellationToken));
        Signature<Func<IVehicleModelRepository, VehicleModel, CancellationToken, Task>>(
            (repository, vehicleModel, cancellationToken) => repository.AddAsync(vehicleModel, cancellationToken));
        Signature<Func<IVehicleModelRepository, VehicleBrandId, VehicleModelName, CancellationToken, Task<bool>>>(
            (repository, vehicleBrandId, name, cancellationToken) => repository.ExistsByNameAsync(vehicleBrandId, name, cancellationToken));
        Signature<Func<IVehicleModelRepository, VehicleBrandId, VehicleModelName, VehicleModelId, CancellationToken, Task<bool>>>(
            (repository, vehicleBrandId, name, excludingVehicleModelId, cancellationToken) => repository.ExistsByNameAsync(vehicleBrandId, name, excludingVehicleModelId, cancellationToken));
        Signature<Func<IVehicleModelRepository, VehicleBrandId, CancellationToken, Task<bool>>>(
            (repository, vehicleBrandId, cancellationToken) => repository.ExistsByVehicleBrandIdAsync(vehicleBrandId, cancellationToken));
        Signature<Action<IVehicleModelRepository, VehicleModel>>(
            (repository, vehicleModel) => repository.Remove(vehicleModel));

        Signature<Func<IVehicleColorRepository, VehicleColorId, CancellationToken, Task<VehicleColor?>>>(
            (repository, id, cancellationToken) => repository.GetByIdAsync(id, cancellationToken));
        Signature<Func<IVehicleColorRepository, int, int, CancellationToken, Task<(IReadOnlyList<VehicleColor> Items, int TotalCount)>>>(
            (repository, page, pageSize, cancellationToken) => repository.ListAsync(page, pageSize, cancellationToken));
        Signature<Func<IVehicleColorRepository, VehicleColor, CancellationToken, Task>>(
            (repository, vehicleColor, cancellationToken) => repository.AddAsync(vehicleColor, cancellationToken));
        Signature<Func<IVehicleColorRepository, VehicleColorName, CancellationToken, Task<bool>>>(
            (repository, name, cancellationToken) => repository.ExistsByNameAsync(name, cancellationToken));
        Signature<Func<IVehicleColorRepository, VehicleColorName, VehicleColorId, CancellationToken, Task<bool>>>(
            (repository, name, excludingVehicleColorId, cancellationToken) => repository.ExistsByNameAsync(name, excludingVehicleColorId, cancellationToken));
        Signature<Action<IVehicleColorRepository, VehicleColor>>(
            (repository, vehicleColor) => repository.Remove(vehicleColor));

        Assert.True(true);
    }

    private static void Signature<TDelegate>(TDelegate _)
    {
    }
}
