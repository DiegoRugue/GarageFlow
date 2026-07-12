using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.Services.Entities;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Adapters.Infrastructure.Customers.Configurations;
using GarageFlow.Adapters.Infrastructure.InventoryItems.Configurations;
using GarageFlow.Adapters.Infrastructure.Services.Configurations;
using GarageFlow.Adapters.Infrastructure.Users.Configurations;
using GarageFlow.Adapters.Infrastructure.Vehicles.Configurations;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Configurations;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Idempotency;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Inbox;
using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GarageFlow.Adapters.Infrastructure.DataAccess;

public sealed class GarageFlowDbContext(DbContextOptions<GarageFlowDbContext> options)
    : DbContext(options), IUnitOfWork
{
    private IDbContextTransaction? _currentTransaction;

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleBrand> VehicleBrands => Set<VehicleBrand>();
    public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
    public DbSet<VehicleColor> VehicleColors => Set<VehicleColor>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<IntakeRequestReceipt> WorkOrderIntakeRequests => Set<IntakeRequestReceipt>();
    public DbSet<EstimateDecisionInboxEvent> EstimateDecisionInboxEvents => Set<EstimateDecisionInboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CustomerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryItemEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleBrandEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleModelEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleColorEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WorkOrderEntityConfiguration());
        modelBuilder.ApplyConfiguration(new EstimateEntityConfiguration());
        modelBuilder.ApplyConfiguration(new EstimateInventoryLineEntityConfiguration());
        modelBuilder.ApplyConfiguration(new EstimateServiceLineEntityConfiguration());
        modelBuilder.ApplyConfiguration(new IntakeRequestReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new EstimateDecisionInboxEventConfiguration());
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

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new BusinessRuleViolationException("A resource with the same unique value already exists.");
        }
    }

    public IReadOnlyList<DomainEvent> DequeueDomainEvents()
    {
        var entities = ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .OfType<IHasDomainEvents>()
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = entities
            .SelectMany(entity => entity.DomainEvents)
            .ToList();

        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }

        return domainEvents;
    }
}
