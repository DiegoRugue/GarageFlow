using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Application.Vehicles.ReadModels;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.Vehicles.Repositories;

public sealed class EfVehicleQueries(GarageFlowDbContext dbContext) : IVehicleQueries
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<VehicleDetailsReadModel?> GetDetailsByIdAsync(VehicleId id, CancellationToken cancellationToken = default)
    {
        return await CreateVehicleDetailsQuery(vehicleId: id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<VehicleDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CustomerId? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = CreateVehicleDetailsQuery(customerId: customerId);
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
        return await CreateVehicleDetailsQuery(customerId: customerId).ToListAsync(cancellationToken);
    }

    private IQueryable<VehicleDetailsReadModel> CreateVehicleDetailsQuery(
        VehicleId? vehicleId = null,
        CustomerId? customerId = null)
    {
        var vehicles = _dbContext.Vehicles.AsNoTracking();

        if (vehicleId is not null)
        {
            vehicles = vehicles.Where(vehicle => vehicle.Id == vehicleId);
        }

        if (customerId is not null)
        {
            vehicles = vehicles.Where(vehicle => vehicle.CustomerId == customerId);
        }

        return from vehicle in vehicles
               join brand in _dbContext.VehicleBrands.AsNoTracking() on vehicle.VehicleBrandId equals brand.Id
               join model in _dbContext.VehicleModels.AsNoTracking()
                   on new { vehicle.VehicleModelId, vehicle.VehicleBrandId }
                   equals new { VehicleModelId = model.Id, model.VehicleBrandId }
               join color in _dbContext.VehicleColors.AsNoTracking() on vehicle.VehicleColorId equals color.Id
               orderby vehicle.CreatedAt, vehicle.Id
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
}
