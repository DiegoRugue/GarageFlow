using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItem;

public sealed class UpdateInventoryItemHandler(
    IInventoryItemRepository inventoryItemRepository) : IRequestHandler<UpdateInventoryItemCommand, UpdateInventoryItemResult>
{
    private readonly IInventoryItemRepository _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));

    public async ValueTask<UpdateInventoryItemResult> Handle(UpdateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var name = InventoryItemName.Create(request.Name);
        var description = Description.Create(request.Description);
        var cost = Price.Create(request.Cost);
        var price = Price.Create(request.Price);

        if (!Enum.IsDefined(typeof(InventoryItemType), request.Type))
        {
            throw new ValidationException($"Inventory item type '{request.Type}' is invalid.");
        }

        var inventoryItemType = (InventoryItemType)request.Type;

        var inventoryItemId = InventoryItemId.From(request.Id);
        var inventoryItem = await _inventoryItemRepository.GetByIdAsync(inventoryItemId, cancellationToken);
        if (inventoryItem is null)
        {
            throw new NotFoundException($"Inventory item with ID '{request.Id}' was not found.");
        }

        inventoryItem.Update(name, description, inventoryItemType, cost, price);

        return new UpdateInventoryItemResult(
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
