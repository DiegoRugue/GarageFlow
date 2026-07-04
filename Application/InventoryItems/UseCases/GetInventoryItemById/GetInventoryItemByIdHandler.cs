using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.InventoryItems.Repositories;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.GetInventoryItemById;

public sealed class GetInventoryItemByIdHandler(
    IInventoryItemRepository inventoryItemRepository) : IRequestHandler<GetInventoryItemByIdQuery, InventoryItemDto>
{
    private readonly IInventoryItemRepository _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));

    public async ValueTask<InventoryItemDto> Handle(GetInventoryItemByIdQuery request, CancellationToken cancellationToken)
    {
        var inventoryItemId = InventoryItemId.From(request.Id);
        var inventoryItem = await _inventoryItemRepository.GetByIdAsync(inventoryItemId, cancellationToken);

        if (inventoryItem is null)
        {
            throw new NotFoundException($"Inventory item with ID '{request.Id}' was not found.");
        }

        return new InventoryItemDto(
            Id: inventoryItem.Id.Value,
            Name: inventoryItem.Name.Value,
            Description: inventoryItem.Description.Value,
            Type: inventoryItem.Type,
            Cost: inventoryItem.Cost.Value,
            Price: inventoryItem.Price.Value,
            StockQuantity: inventoryItem.StockQuantity.Value,
            CreatedAt: inventoryItem.CreatedAt);
    }
}
