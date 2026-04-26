using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Infrastructure.Vehicles.Repositories;

public sealed class VehicleBrandRepository(GarageFlowDbContext dbContext) : IVehicleBrandRepository
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<VehicleBrand?> GetByIdAsync(
        VehicleBrandId id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.VehicleBrands.FirstOrDefaultAsync(
            vehicleBrand => vehicleBrand.Id == id,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<VehicleBrand> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.VehicleBrands
            .AsNoTracking()
            .OrderBy(vehicleBrand => vehicleBrand.CreatedAt)
            .ThenBy(vehicleBrand => vehicleBrand.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task AddAsync(VehicleBrand vehicleBrand, CancellationToken cancellationToken = default)
    {
        return _dbContext.VehicleBrands.AddAsync(vehicleBrand, cancellationToken).AsTask();
    }

    public async Task<bool> ExistsByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.ToUpper();

        return await _dbContext.VehicleBrands
            .AsNoTracking()
            .AnyAsync(vehicleBrand => vehicleBrand.Name.ToUpper() == normalizedName, cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(
        string name,
        VehicleBrandId excludingVehicleBrandId,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.ToUpper();

        return await _dbContext.VehicleBrands
            .AsNoTracking()
            .AnyAsync(
                vehicleBrand =>
                    vehicleBrand.Name.ToUpper() == normalizedName &&
                    vehicleBrand.Id != excludingVehicleBrandId,
                cancellationToken);
    }

    public void Remove(VehicleBrand vehicleBrand)
    {
        _dbContext.VehicleBrands.Remove(vehicleBrand);
    }
}
