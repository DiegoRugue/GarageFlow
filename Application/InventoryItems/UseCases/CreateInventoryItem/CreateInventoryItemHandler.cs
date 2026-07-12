using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Application.InventoryItems.Common;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.CreateInventoryItem;

public sealed class CreateInventoryItemHandler(
    IInventoryItemRepository inventoryItemRepository) : IRequestHandler<CreateInventoryItemCommand, CreateInventoryItemResult>
{
    private readonly IInventoryItemRepository _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));

    public async ValueTask<CreateInventoryItemResult> Handle(CreateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var name = InventoryItemName.Create(request.Name);
        var description = Description.Create(request.Description);
        var cost = Price.Create(request.Cost);
        var price = Price.Create(request.Price);
        var stockQuantity = InventoryItemStockQuantity.Create(request.StockQuantity);

        var inventoryItemType = InventoryItemTypeParser.Parse(request.Type);

        var inventoryItem = InventoryItem.Create(
            name,
            description,
            inventoryItemType,
            cost,
            price,
            stockQuantity);

        await _inventoryItemRepository.AddAsync(inventoryItem, cancellationToken);

        return new CreateInventoryItemResult(
            Id: inventoryItem.Id.Value,
            Name: inventoryItem.Name.Value,
            Description: inventoryItem.Description.Value,
            Type: inventoryItem.Type.ToString(),
            Cost: inventoryItem.Cost.Value,
            Price: inventoryItem.Price.Value,
            StockQuantity: inventoryItem.StockQuantity.Value,
            CreatedAt: inventoryItem.CreatedAt);
    }
}
