using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Infrastructure.Vehicles.Repositories;

public sealed class VehicleRepository(GarageFlowDbContext dbContext) : IVehicleRepository
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<Vehicle?> GetByIdAsync(VehicleId id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehicles.FirstOrDefaultAsync(
            vehicle => vehicle.Id == id,
            cancellationToken);
    }

    public async Task<VehicleDetailsReadModel?> GetDetailsByIdAsync(VehicleId id, CancellationToken cancellationToken = default)
    {
        var query = CreateVehicleDetailsQuery();

        return await query.FirstOrDefaultAsync(
            vehicle => vehicle.Id == id.Value,
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

    public async Task<(IReadOnlyList<VehicleDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CustomerId? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = CreateVehicleDetailsQuery();

        if (customerId is not null)
        {
            query = query.Where(vehicle => vehicle.CustomerId == customerId.Value.Value);
        }

        query = query.OrderBy(vehicle => vehicle.CreatedAt)
            .ThenBy(vehicle => vehicle.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<VehicleDetailsReadModel>> ListDetailsByCustomerIdAsync(
        CustomerId customerId,
        CancellationToken cancellationToken = default)
    {
        var query = CreateVehicleDetailsQuery()
            .Where(vehicle => vehicle.CustomerId == customerId.Value)
            .OrderBy(vehicle => vehicle.CreatedAt)
            .ThenBy(vehicle => vehicle.Id);

        return await query.ToListAsync(cancellationToken);
    }

    private IQueryable<VehicleDetailsReadModel> CreateVehicleDetailsQuery()
    {
        return from vehicle in _dbContext.Vehicles.AsNoTracking()
            join brand in _dbContext.VehicleBrands.AsNoTracking() on vehicle.VehicleBrandId equals brand.Id
            join model in _dbContext.VehicleModels.AsNoTracking()
                on new { VehicleModelId = vehicle.VehicleModelId, VehicleBrandId = vehicle.VehicleBrandId }
                equals new { VehicleModelId = model.Id, VehicleBrandId = model.VehicleBrandId }
            join color in _dbContext.VehicleColors.AsNoTracking() on vehicle.VehicleColorId equals color.Id
            select new VehicleDetailsReadModel(
                vehicle.Id.Value,
                vehicle.CustomerId.Value,
                vehicle.Year.Value,
                vehicle.VehicleBrandId.Value,
                EF.Property<string>(brand, nameof(VehicleBrand.Name)),
                vehicle.VehicleModelId.Value,
                EF.Property<string>(model, nameof(VehicleModel.Name)),
                vehicle.VehicleColorId.Value,
                EF.Property<string>(color, nameof(VehicleColor.Name)),
                vehicle.LicensePlate.Value,
                vehicle.CreatedAt);
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
