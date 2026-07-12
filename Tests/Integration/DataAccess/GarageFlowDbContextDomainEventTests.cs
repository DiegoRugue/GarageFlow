using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Domain.Services.Events;
using GarageFlow.Tests.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GarageFlow.Tests.Integration.DataAccess;

public sealed class GarageFlowDbContextDomainEventTests
{
    [Fact]
    public async Task SaveChangesAsync_ShouldStageEventFromDeletedEntity_AfterEntityIsDetached()
    {
        await using var dbContext = CreateDbContext();
        var service = new ServiceBuilder().Build();
        dbContext.Services.Add(service);
        await dbContext.SaveChangesAsync();
        _ = dbContext.DequeueDomainEvents();
        service.Delete();
        dbContext.Services.Remove(service);

        await dbContext.SaveChangesAsync();

        Assert.Equal(EntityState.Detached, dbContext.Entry(service).State);
        Assert.IsType<ServiceDeleted>(Assert.Single(dbContext.DequeueDomainEvents()));
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldRetainEntityEvents_WhenBaseSaveFails()
    {
        var interceptor = new FailFirstSaveInterceptor();
        await using var dbContext = CreateDbContext(interceptor);
        var service = new ServiceBuilder().Build();
        dbContext.Services.Add(service);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        Assert.Single(service.DomainEvents);
        Assert.Empty(dbContext.DequeueDomainEvents());

        await dbContext.SaveChangesAsync();

        Assert.Empty(service.DomainEvents);
        Assert.Single(dbContext.DequeueDomainEvents());
    }

    [Fact]
    public async Task RollbackTransactionAsync_ShouldClearTrackedEntitiesAndStagedEvents()
    {
        await using var dbContext = CreateDbContext();
        var service = new ServiceBuilder().Build();
        dbContext.Services.Add(service);
        await dbContext.SaveChangesAsync();

        await dbContext.RollbackTransactionAsync();

        Assert.Empty(dbContext.ChangeTracker.Entries());
        Assert.Empty(dbContext.DequeueDomainEvents());
    }

    [Fact]
    public async Task CommitTransactionAsync_ShouldNotSave_WhenThereIsNoRelationalTransaction()
    {
        var databaseName = $"garageflow-commit-only-tests-{Guid.NewGuid():N}";
        await using var dbContext = CreateDbContext(databaseName: databaseName);
        dbContext.Services.Add(new ServiceBuilder().Build());

        await dbContext.CommitTransactionAsync();

        await using var verificationContext = CreateDbContext(databaseName: databaseName);
        Assert.Empty(await verificationContext.Services.ToListAsync());
    }

    private static GarageFlowDbContext CreateDbContext(
        SaveChangesInterceptor? interceptor = null,
        string? databaseName = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"garageflow-domain-event-tests-{Guid.NewGuid():N}");
        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new GarageFlowDbContext(optionsBuilder.Options);
    }

    private sealed class FailFirstSaveInterceptor : SaveChangesInterceptor
    {
        private bool _hasFailed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_hasFailed)
            {
                _hasFailed = true;
                throw new DbUpdateException("Expected save failure.");
            }

            return ValueTask.FromResult(result);
        }
    }
}
