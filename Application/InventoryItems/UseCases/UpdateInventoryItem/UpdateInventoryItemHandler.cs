using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.InventoryItems.Repositories;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItem;

public sealed class UpdateInventoryItemHandler(
    IInventoryItemRepository inventoryItemRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateInventoryItemCommand, UpdateInventoryItemResult>
{
    private readonly IInventoryItemRepository _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<UpdateInventoryItemResult> Handle(UpdateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var name = InventoryItemName.Create(request.Name);
        var description = Description.Create(request.Description);
        var cost = Price.Create(request.Cost);
        var price = Price.Create(request.Price);

        var inventoryItemId = InventoryItemId.From(request.Id);
        var inventoryItem = await _inventoryItemRepository.GetByIdAsync(inventoryItemId, cancellationToken);
        if (inventoryItem is null)
        {
            throw new NotFoundException($"Inventory item with ID '{request.Id}' was not found.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            inventoryItem.Update(name, description, request.Type, cost, price);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new UpdateInventoryItemResult(
                Id: inventoryItem.Id.Value,
                Name: inventoryItem.Name.Value,
                Description: inventoryItem.Description.Value,
                Type: inventoryItem.Type,
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
