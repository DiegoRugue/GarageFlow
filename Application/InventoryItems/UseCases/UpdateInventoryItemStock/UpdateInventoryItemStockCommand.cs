using Mediator;

namespace GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItemStock;

public sealed record UpdateInventoryItemStockCommand(
    Guid Id,
    int StockQuantity) : IRequest<UpdateInventoryItemStockResult>;
