using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.Vehicles.Repositories;

public sealed class VehicleColorRepository(GarageFlowDbContext dbContext) : IVehicleColorRepository
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<VehicleColor?> GetByIdAsync(
        VehicleColorId id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.VehicleColors.FirstOrDefaultAsync(
            vehicleColor => vehicleColor.Id == id,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<VehicleColor> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.VehicleColors
            .AsNoTracking()
            .OrderBy(vehicleColor => vehicleColor.CreatedAt)
            .ThenBy(vehicleColor => vehicleColor.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task AddAsync(VehicleColor vehicleColor, CancellationToken cancellationToken = default)
    {
        return _dbContext.VehicleColors.AddAsync(vehicleColor, cancellationToken).AsTask();
    }

    public async Task<bool> ExistsByNameAsync(
        VehicleColorName name,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsNpgsql())
        {
            var existingNames = await _dbContext.VehicleColors
                .AsNoTracking()
                .Select(vehicleColor => vehicleColor.Name)
                .ToListAsync(cancellationToken);

            return existingNames.Any(existingName =>
                string.Equals(existingName.Value, name.Value, StringComparison.OrdinalIgnoreCase));
        }

        return await _dbContext.Database
            .SqlQuery<Guid>($@"SELECT ""Id"" AS ""Value"" FROM ""VehicleColors"" WHERE upper(""Name"") = upper({name.Value})")
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(
        VehicleColorName name,
        VehicleColorId excludingVehicleColorId,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsNpgsql())
        {
            var existingColors = await _dbContext.VehicleColors
                .AsNoTracking()
                .Select(vehicleColor => new { vehicleColor.Id, vehicleColor.Name })
                .ToListAsync(cancellationToken);

            return existingColors.Any(existingColor =>
                existingColor.Id != excludingVehicleColorId
                && string.Equals(existingColor.Name.Value, name.Value, StringComparison.OrdinalIgnoreCase));
        }

        return await _dbContext.Database
            .SqlQuery<Guid>(
                $@"SELECT ""Id"" AS ""Value"" FROM ""VehicleColors"" WHERE upper(""Name"") = upper({name.Value}) AND ""Id"" <> {excludingVehicleColorId.Value}")
            .AnyAsync(cancellationToken);
    }

    public void Remove(VehicleColor vehicleColor)
    {
        _dbContext.VehicleColors.Remove(vehicleColor);
    }
}
