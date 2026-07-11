using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.DeleteInventoryItem;

public sealed class DeleteInventoryItemHandler(
    IInventoryItemRepository inventoryItemRepository) : IRequestHandler<DeleteInventoryItemCommand, Unit>
{
    private readonly IInventoryItemRepository _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));

    public async ValueTask<Unit> Handle(DeleteInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var inventoryItemId = InventoryItemId.From(request.Id);
        var inventoryItem = await _inventoryItemRepository.GetByIdAsync(inventoryItemId, cancellationToken);
        if (inventoryItem is null)
        {
            throw new NotFoundException($"Inventory item with ID '{request.Id}' was not found.");
        }

        inventoryItem.Delete();
        _inventoryItemRepository.Remove(inventoryItem);

        return Unit.Value;
    }
}
