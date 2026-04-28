using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.ValueObjects;

namespace GarageFlow.Domain.InventoryItems.Repositories;

public interface IInventoryItemRepository
{
    Task<InventoryItem?> GetByIdAsync(
        InventoryItemId id,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<InventoryItem> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<InventoryItemDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default);

    void Remove(InventoryItem item);
}
