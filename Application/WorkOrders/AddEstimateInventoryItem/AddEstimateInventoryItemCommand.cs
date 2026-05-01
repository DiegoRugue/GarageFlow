using Mediator;

namespace GarageFlow.Application.WorkOrders.AddEstimateInventoryItem;

public sealed record AddEstimateInventoryItemCommand(
    Guid WorkOrderId,
    Guid EstimateId,
    Guid InventoryItemId,
    int Quantity) : IRequest<AddEstimateInventoryItemResult>;
