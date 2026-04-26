using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Infrastructure.Customers.Configurations;
using GarageFlow.Infrastructure.Vehicles.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GarageFlow.Infrastructure.DataAccess;

public sealed class GarageFlowDbContext(DbContextOptions<GarageFlowDbContext> options)
    : DbContext(options), IUnitOfWork
{
    private IDbContextTransaction? _currentTransaction;

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleBrand> VehicleBrands => Set<VehicleBrand>();
    public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
    public DbSet<VehicleColor> VehicleColors => Set<VehicleColor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CustomerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleBrandEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleModelEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleColorEntityConfiguration());
        base.OnModelCreating(modelBuilder);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null || !Database.IsRelational())
        {
            return;
        }

        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            await SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            await SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            ChangeTracker.Clear();
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
            ChangeTracker.Clear();
        }
    }

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }
}
