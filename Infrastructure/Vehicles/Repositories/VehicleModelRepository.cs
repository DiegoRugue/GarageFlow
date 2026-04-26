using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Infrastructure.Vehicles.Repositories;

public sealed class VehicleModelRepository(GarageFlowDbContext dbContext) : IVehicleModelRepository
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<VehicleModel?> GetByIdAsync(
        VehicleModelId id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.VehicleModels.FirstOrDefaultAsync(
            vehicleModel => vehicleModel.Id == id,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<VehicleModel> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.VehicleModels
            .AsNoTracking()
            .OrderBy(vehicleModel => vehicleModel.CreatedAt)
            .ThenBy(vehicleModel => vehicleModel.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<VehicleModel> Items, int TotalCount)> ListByVehicleBrandIdAsync(
        VehicleBrandId vehicleBrandId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.VehicleModels
            .AsNoTracking()
            .Where(vehicleModel => vehicleModel.VehicleBrandId == vehicleBrandId)
            .OrderBy(vehicleModel => vehicleModel.CreatedAt)
            .ThenBy(vehicleModel => vehicleModel.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task AddAsync(VehicleModel vehicleModel, CancellationToken cancellationToken = default)
    {
        return _dbContext.VehicleModels.AddAsync(vehicleModel, cancellationToken).AsTask();
    }

    public async Task<bool> ExistsByNameAsync(
        VehicleBrandId vehicleBrandId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.ToUpper();

        return await _dbContext.VehicleModels
            .AsNoTracking()
            .AnyAsync(
                vehicleModel =>
                    vehicleModel.VehicleBrandId == vehicleBrandId &&
                    vehicleModel.Name.ToUpper() == normalizedName,
                cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(
        VehicleBrandId vehicleBrandId,
        string name,
        VehicleModelId excludingVehicleModelId,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.ToUpper();

        return await _dbContext.VehicleModels
            .AsNoTracking()
            .AnyAsync(
                vehicleModel =>
                    vehicleModel.VehicleBrandId == vehicleBrandId &&
                    vehicleModel.Name.ToUpper() == normalizedName &&
                    vehicleModel.Id != excludingVehicleModelId,
                cancellationToken);
    }

    public async Task<bool> ExistsByVehicleBrandIdAsync(
        VehicleBrandId vehicleBrandId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.VehicleModels
            .AsNoTracking()
            .AnyAsync(vehicleModel => vehicleModel.VehicleBrandId == vehicleBrandId, cancellationToken);
    }

    public void Remove(VehicleModel vehicleModel)
    {
        _dbContext.VehicleModels.Remove(vehicleModel);
    }
}
