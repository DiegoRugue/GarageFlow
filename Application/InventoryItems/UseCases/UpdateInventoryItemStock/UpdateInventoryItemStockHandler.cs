using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItemStock;

public sealed class UpdateInventoryItemStockHandler(
    IInventoryItemRepository inventoryItemRepository) : IRequestHandler<UpdateInventoryItemStockCommand, UpdateInventoryItemStockResult>
{
    private readonly IInventoryItemRepository _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));

    public async ValueTask<UpdateInventoryItemStockResult> Handle(UpdateInventoryItemStockCommand request, CancellationToken cancellationToken)
    {
        var stockQuantity = InventoryItemStockQuantity.Create(request.StockQuantity);
        var inventoryItemId = InventoryItemId.From(request.Id);
        var inventoryItem = await _inventoryItemRepository.GetByIdAsync(inventoryItemId, cancellationToken);
        if (inventoryItem is null)
        {
            throw new NotFoundException($"Inventory item with ID '{request.Id}' was not found.");
        }

        inventoryItem.SetStockQuantity(stockQuantity);

        return new UpdateInventoryItemStockResult(
            Id: inventoryItem.Id.Value,
            Name: inventoryItem.Name.Value,
            Description: inventoryItem.Description.Value,
            Type: (int)inventoryItem.Type,
            Cost: inventoryItem.Cost.Value,
            Price: inventoryItem.Price.Value,
            StockQuantity: inventoryItem.StockQuantity.Value,
            CreatedAt: inventoryItem.CreatedAt);
    }
}
