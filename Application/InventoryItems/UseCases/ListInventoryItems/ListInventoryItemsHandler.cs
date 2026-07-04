using GarageFlow.Application.InventoryItems.UseCases.GetInventoryItemById;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.InventoryItems.Repositories;
using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.ListInventoryItems;

public sealed class ListInventoryItemsHandler(
    IInventoryItemRepository inventoryItemRepository) : IRequestHandler<ListInventoryItemsQuery, ListInventoryItemsResult>
{
    private const int MaxPageSize = 100;

    private readonly IInventoryItemRepository _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));

    public async ValueTask<ListInventoryItemsResult> Handle(ListInventoryItemsQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1)
        {
            throw new ValidationException($"Page must be greater than or equal to 1. Received: {request.Page}.");
        }

        if (request.PageSize < 1)
        {
            throw new ValidationException($"PageSize must be greater than or equal to 1. Received: {request.PageSize}.");
        }

        if (request.PageSize > MaxPageSize)
        {
            throw new ValidationException($"PageSize cannot exceed {MaxPageSize}. Received: {request.PageSize}.");
        }

        var (items, totalCount) = await _inventoryItemRepository.ListAsync(request.Page, request.PageSize, cancellationToken);

        if (totalCount == 0)
        {
            return new ListInventoryItemsResult(
                Items: [],
                TotalCount: 0,
                Page: request.Page,
                PageSize: request.PageSize);
        }

        var dtos = items.Select(inventoryItem => new InventoryItemDto(
            Id: inventoryItem.Id.Value,
            Name: inventoryItem.Name.Value,
            Description: inventoryItem.Description.Value,
            Type: inventoryItem.Type,
            Cost: inventoryItem.Cost.Value,
            Price: inventoryItem.Price.Value,
            StockQuantity: inventoryItem.StockQuantity.Value,
            CreatedAt: inventoryItem.CreatedAt)).ToList();

        return new ListInventoryItemsResult(
            Items: dtos,
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);
    }
}
