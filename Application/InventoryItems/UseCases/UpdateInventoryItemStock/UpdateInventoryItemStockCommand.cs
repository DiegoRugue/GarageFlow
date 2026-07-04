using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItemStock;

public sealed record UpdateInventoryItemStockCommand(
    Guid Id,
    int StockQuantity) : ICommand<UpdateInventoryItemStockResult>;
