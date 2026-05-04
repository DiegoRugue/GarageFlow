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
        VehicleBrandName name,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsNpgsql())
        {
            var existingNames = await _dbContext.VehicleBrands
                .AsNoTracking()
                .Select(vehicleBrand => vehicleBrand.Name)
                .ToListAsync(cancellationToken);

            return existingNames.Any(existingName =>
                string.Equals(existingName.Value, name.Value, StringComparison.OrdinalIgnoreCase));
        }

        return await _dbContext.Database
            .SqlQuery<Guid>($@"SELECT ""Id"" AS ""Value"" FROM ""VehicleBrands"" WHERE upper(""Name"") = upper({name.Value})")
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(
        VehicleBrandName name,
        VehicleBrandId excludingVehicleBrandId,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsNpgsql())
        {
            var existingBrands = await _dbContext.VehicleBrands
                .AsNoTracking()
                .Select(vehicleBrand => new { vehicleBrand.Id, vehicleBrand.Name })
                .ToListAsync(cancellationToken);

            return existingBrands.Any(existingBrand =>
                existingBrand.Id != excludingVehicleBrandId
                && string.Equals(existingBrand.Name.Value, name.Value, StringComparison.OrdinalIgnoreCase));
        }

        return await _dbContext.Database
            .SqlQuery<Guid>(
                $@"SELECT ""Id"" AS ""Value"" FROM ""VehicleBrands"" WHERE upper(""Name"") = upper({name.Value}) AND ""Id"" <> {excludingVehicleBrandId.Value}")
            .AnyAsync(cancellationToken);
    }

    public void Remove(VehicleBrand vehicleBrand)
    {
        _dbContext.VehicleBrands.Remove(vehicleBrand);
    }
}
