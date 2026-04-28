using Mediator;

namespace GarageFlow.Application.InventoryItems.UpdateInventoryItemStock;

public sealed record UpdateInventoryItemStockCommand(
    Guid Id,
    int StockQuantity) : IRequest<UpdateInventoryItemStockResult>;
