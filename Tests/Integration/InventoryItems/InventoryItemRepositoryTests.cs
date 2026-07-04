using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Adapters.Infrastructure.InventoryItems.Repositories;
using GarageFlow.Tests.Shared.InventoryItems;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Tests.Integration.InventoryItems;

public sealed class InventoryItemRepositoryTests
{
    [Fact]
    public async Task GetByIdForStockReservationAsync_ShouldReturnItem_WhenUsingInMemoryProvider()
    {
        using var dbContext = CreateDbContext();
        var repository = new InventoryItemRepository(dbContext);
        var item = new InventoryItemBuilder()
            .WithName("Brake Fluid")
            .Build();

        await repository.AddAsync(item);
        await dbContext.SaveChangesAsync();

        var result = await repository.GetByIdForStockReservationAsync(item.Id);

        Assert.NotNull(result);
        Assert.Equal(item.Id, result.Id);
    }

    [Fact]
    public async Task ListAsync_ShouldReturnPagedItemsAndTotalCount()
    {
        using var dbContext = CreateDbContext();
        var repository = new InventoryItemRepository(dbContext);
        var firstItem = new InventoryItemBuilder()
            .WithName("Air Filter")
            .Build();
        var secondItem = new InventoryItemBuilder()
            .WithName("Oil Filter")
            .Build();
        var thirdItem = new InventoryItemBuilder()
            .WithName("Spark Plug")
            .Build();

        await repository.AddAsync(firstItem);
        await repository.AddAsync(secondItem);
        await repository.AddAsync(thirdItem);
        await dbContext.SaveChangesAsync();

        var (items, totalCount) = await repository.ListAsync(page: 2, pageSize: 2);

        Assert.Equal(3, totalCount);
        var item = Assert.Single(items);
        Assert.Equal(thirdItem.Id, item.Id);
    }

    [Fact]
    public async Task ListDetailsAsync_ShouldReturnProjectedReadModels()
    {
        using var dbContext = CreateDbContext();
        var repository = new InventoryItemRepository(dbContext);
        var queries = new EfInventoryItemQueries(dbContext);
        var item = new InventoryItemBuilder()
            .WithName("Cabin Filter")
            .WithDescription("Activated carbon cabin filter")
            .WithType(InventoryItemType.Part)
            .WithCost(21.50m)
            .WithPrice(49.90m)
            .WithStockQuantity(8)
            .Build();

        await repository.AddAsync(item);
        await dbContext.SaveChangesAsync();

        var (items, totalCount) = await queries.ListDetailsAsync(page: 1, pageSize: 10);

        Assert.Equal(1, totalCount);
        var details = Assert.Single(items);
        Assert.Equal(item.Id.Value, details.Id);
        Assert.Equal("Cabin Filter", details.Name);
        Assert.Equal("Activated carbon cabin filter", details.Description);
        Assert.Equal(InventoryItemType.Part, details.Type);
        Assert.Equal(21.50m, details.Cost);
        Assert.Equal(49.90m, details.Price);
        Assert.Equal(8, details.StockQuantity);
        Assert.Equal(item.CreatedAt, details.CreatedAt);
    }

    [Fact]
    public async Task Remove_ShouldDeleteItem()
    {
        using var dbContext = CreateDbContext();
        var repository = new InventoryItemRepository(dbContext);
        var item = new InventoryItemBuilder().Build();

        await repository.AddAsync(item);
        await dbContext.SaveChangesAsync();

        repository.Remove(item);
        await dbContext.SaveChangesAsync();

        var result = await repository.GetByIdAsync(item.Id);

        Assert.Null(result);
    }

    private static GarageFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseInMemoryDatabase($"garageflow-inventory-item-repository-tests-{Guid.NewGuid():N}")
            .Options;

        return new GarageFlowDbContext(options);
    }
}
