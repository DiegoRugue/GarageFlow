using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Application.InventoryItems.ReadModels;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.InventoryItems.Repositories;

public sealed class EfInventoryItemQueries(GarageFlowDbContext dbContext) : IInventoryItemQueries
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<(IReadOnlyList<InventoryItemDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.InventoryItems.AsNoTracking()
            .OrderBy(inventoryItem => inventoryItem.CreatedAt)
            .ThenBy(inventoryItem => inventoryItem.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var readModels = items.Select(inventoryItem => new InventoryItemDetailsReadModel(
            inventoryItem.Id.Value,
            inventoryItem.Name.Value,
            inventoryItem.Description.Value,
            inventoryItem.Type,
            inventoryItem.Cost.Value,
            inventoryItem.Price.Value,
            inventoryItem.StockQuantity.Value,
            inventoryItem.CreatedAt))
            .ToList();

        return (readModels, totalCount);
    }
}
