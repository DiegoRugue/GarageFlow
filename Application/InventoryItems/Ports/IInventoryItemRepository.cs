using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.ValueObjects;

namespace GarageFlow.Application.InventoryItems.Ports;

public interface IInventoryItemRepository
{
    Task<InventoryItem?> GetByIdAsync(
        InventoryItemId id,
        CancellationToken cancellationToken = default);

    Task<InventoryItem?> GetByIdForStockReservationAsync(
        InventoryItemId id,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<InventoryItem> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default);

    void Remove(InventoryItem item);
}
