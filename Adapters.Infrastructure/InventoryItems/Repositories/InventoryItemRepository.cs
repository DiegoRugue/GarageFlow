using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Application.InventoryItems.ReadModels;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.InventoryItems.Repositories;

public sealed class InventoryItemRepository(GarageFlowDbContext dbContext) : IInventoryItemRepository
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<InventoryItem?> GetByIdAsync(
        InventoryItemId id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.InventoryItems.FirstOrDefaultAsync(
            inventoryItem => inventoryItem.Id == id,
            cancellationToken);
    }

    public async Task<InventoryItem?> GetByIdForStockReservationAsync(
        InventoryItemId id,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsRelational() || !_dbContext.Database.IsNpgsql())
        {
            return await GetByIdAsync(id, cancellationToken);
        }

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM "InventoryItems" WHERE "Id" = {id.Value} FOR UPDATE""",
            cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<(IReadOnlyList<InventoryItem> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.InventoryItems
            .AsNoTracking()
            .OrderBy(inventoryItem => inventoryItem.CreatedAt)
            .ThenBy(inventoryItem => inventoryItem.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

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

    public Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        return _dbContext.InventoryItems.AddAsync(item, cancellationToken).AsTask();
    }

    public void Remove(InventoryItem item)
    {
        _dbContext.InventoryItems.Remove(item);
    }
}
