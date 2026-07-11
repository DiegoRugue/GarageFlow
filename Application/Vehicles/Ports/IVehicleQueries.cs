using GarageFlow.Application.Vehicles.ReadModels;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Application.Vehicles.Ports;

public interface IVehicleQueries
{
    Task<VehicleDetailsReadModel?> GetDetailsByIdAsync(
        VehicleId id,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<VehicleDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CustomerId? customerId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleDetailsReadModel>> ListDetailsByCustomerIdAsync(
        CustomerId customerId,
        CancellationToken cancellationToken = default);
}
