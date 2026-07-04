using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.Vehicles.Repositories;

public sealed class VehicleRepository(GarageFlowDbContext dbContext) : IVehicleRepository
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<Vehicle?> GetByIdAsync(VehicleId id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehicles.FirstOrDefaultAsync(
            vehicle => vehicle.Id == id,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Vehicles
            .AsNoTracking()
            .OrderBy(vehicle => vehicle.CreatedAt)
            .ThenBy(vehicle => vehicle.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        return _dbContext.Vehicles.AddAsync(vehicle, cancellationToken).AsTask();
    }

    public async Task<bool> ExistsByLicensePlateAsync(
        LicensePlate licensePlate,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehicles
            .AsNoTracking()
            .AnyAsync(vehicle => vehicle.LicensePlate == licensePlate, cancellationToken);
    }

    public async Task<bool> ExistsByLicensePlateAsync(
        LicensePlate licensePlate,
        VehicleId excludingVehicleId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehicles
            .AsNoTracking()
            .AnyAsync(
                vehicle => vehicle.LicensePlate == licensePlate && vehicle.Id != excludingVehicleId,
                cancellationToken);
    }

    public async Task<bool> ExistsByVehicleBrandIdAsync(
        VehicleBrandId vehicleBrandId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehicles
            .AsNoTracking()
            .AnyAsync(vehicle => vehicle.VehicleBrandId == vehicleBrandId, cancellationToken);
    }

    public async Task<bool> ExistsByVehicleModelIdAsync(
        VehicleModelId vehicleModelId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehicles
            .AsNoTracking()
            .AnyAsync(vehicle => vehicle.VehicleModelId == vehicleModelId, cancellationToken);
    }

    public async Task<bool> ExistsByVehicleColorIdAsync(
        VehicleColorId vehicleColorId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehicles
            .AsNoTracking()
            .AnyAsync(vehicle => vehicle.VehicleColorId == vehicleColorId, cancellationToken);
    }

    public async Task<bool> ExistsByCustomerIdAsync(
        CustomerId customerId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehicles
            .AsNoTracking()
            .AnyAsync(vehicle => vehicle.CustomerId == customerId, cancellationToken);
    }

    public void Remove(Vehicle vehicle)
    {
        _dbContext.Vehicles.Remove(vehicle);
    }
}
