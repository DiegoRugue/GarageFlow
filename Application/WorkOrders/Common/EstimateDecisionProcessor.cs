using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.Application.WorkOrders.Common;

public sealed class EstimateDecisionProcessor(IInventoryItemRepository inventoryItemRepository)
{
    private readonly IInventoryItemRepository _inventoryItemRepository = inventoryItemRepository
        ?? throw new ArgumentNullException(nameof(inventoryItemRepository));

    public void Approve(WorkOrder workOrder, EstimateId estimateId) =>
        workOrder.ApproveEstimate(estimateId);

    public Task RejectAsync(
        WorkOrder workOrder,
        EstimateId estimateId,
        CancellationToken cancellationToken) =>
        ReleaseAsync(workOrder.RejectEstimate(estimateId), cancellationToken);

    public async Task ReleaseAsync(
        IReadOnlyCollection<InventoryReservation> reservations,
        CancellationToken cancellationToken)
    {
        foreach (var reservation in reservations.OrderBy(item => item.InventoryItemId.Value))
        {
            var item = await _inventoryItemRepository.GetByIdForStockReservationAsync(
                reservation.InventoryItemId,
                cancellationToken);
            if (item is null)
            {
                throw new NotFoundException(
                    $"Inventory item with ID '{reservation.InventoryItemId.Value}' was not found.");
            }

            item.IncreaseStock(reservation.Quantity.Value);
        }
    }
}
