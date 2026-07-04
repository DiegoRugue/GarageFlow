using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.CreateInventoryItem;

public sealed class CreateInventoryItemHandler(
    IInventoryItemRepository inventoryItemRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateInventoryItemCommand, CreateInventoryItemResult>
{
    private readonly IInventoryItemRepository _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<CreateInventoryItemResult> Handle(CreateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var name = InventoryItemName.Create(request.Name);
        var description = Description.Create(request.Description);
        var cost = Price.Create(request.Cost);
        var price = Price.Create(request.Price);
        var stockQuantity = InventoryItemStockQuantity.Create(request.StockQuantity);

        if (!Enum.IsDefined(typeof(InventoryItemType), request.Type))
        {
            throw new ValidationException($"Inventory item type '{request.Type}' is invalid.");
        }

        var inventoryItemType = (InventoryItemType)request.Type;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var inventoryItem = InventoryItem.Create(
                name,
                description,
                inventoryItemType,
                cost,
                price,
                stockQuantity);

            await _inventoryItemRepository.AddAsync(inventoryItem, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new CreateInventoryItemResult(
                Id: inventoryItem.Id.Value,
                Name: inventoryItem.Name.Value,
                Description: inventoryItem.Description.Value,
                Type: (int)inventoryItem.Type,
                Cost: inventoryItem.Cost.Value,
                Price: inventoryItem.Price.Value,
                StockQuantity: inventoryItem.StockQuantity.Value,
                CreatedAt: inventoryItem.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
