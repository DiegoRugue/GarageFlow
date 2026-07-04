namespace GarageFlow.Application.WorkOrders.ReadModels;

public sealed record WorkOrderInventoryLineReadModel(
    Guid Id,
    Guid EstimateId,
    Guid InventoryItemId,
    string DescriptionSnapshot,
    int Quantity,
    decimal UnitCost,
    decimal UnitPrice,
    decimal TotalPrice);
