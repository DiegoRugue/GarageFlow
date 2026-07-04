using GarageFlow.Application.InventoryItems.ReadModels;

namespace GarageFlow.Application.InventoryItems.Ports;

public interface IInventoryItemQueries
{
    Task<(IReadOnlyList<InventoryItemDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
