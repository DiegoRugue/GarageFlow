using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.AddEstimateInventoryItem;

public sealed class AddEstimateInventoryItemHandler(
    IWorkOrderRepository workOrderRepository,
    IInventoryItemRepository inventoryItemRepository) : IRequestHandler<AddEstimateInventoryItemCommand, AddEstimateInventoryItemResult>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly IInventoryItemRepository _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));

    public async ValueTask<AddEstimateInventoryItemResult> Handle(AddEstimateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var estimateId = EstimateId.From(request.EstimateId);
        var inventoryItemId = InventoryItemId.From(request.InventoryItemId);
        var quantity = EstimateItemQuantity.Create(request.Quantity);

        var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
        }

        workOrder.EnsureEstimateCanBeEdited(estimateId);

        var inventoryItem = await _inventoryItemRepository.GetByIdForStockReservationAsync(inventoryItemId, cancellationToken);
        if (inventoryItem is null)
        {
            throw new NotFoundException($"Inventory item with ID '{request.InventoryItemId}' was not found.");
        }

        inventoryItem.DecreaseStock(quantity.Value);
        workOrder.AddInventoryLine(
            estimateId,
            inventoryItem.Id,
            inventoryItem.Description,
            quantity,
            inventoryItem.Cost,
            inventoryItem.Price);

        return new AddEstimateInventoryItemResult(
            EstimateId: estimateId.Value,
            InventoryItemId: inventoryItem.Id.Value,
            Description: inventoryItem.Description.Value,
            Quantity: quantity.Value,
            UnitCost: inventoryItem.Cost.Value,
            UnitPrice: inventoryItem.Price.Value,
            TotalPrice: inventoryItem.Price.Value * quantity.Value);
    }
}
