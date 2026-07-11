namespace GarageFlow.Adapters.Api.WorkOrders.Responses;

public sealed record CustomerWorkOrderInventoryLineResponse(
    Guid Id,
    Guid EstimateId,
    Guid InventoryItemId,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);
