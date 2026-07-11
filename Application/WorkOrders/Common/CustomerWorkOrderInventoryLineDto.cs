namespace GarageFlow.Application.WorkOrders.Common;

public sealed record CustomerWorkOrderInventoryLineDto(
    Guid Id,
    Guid EstimateId,
    Guid InventoryItemId,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);
