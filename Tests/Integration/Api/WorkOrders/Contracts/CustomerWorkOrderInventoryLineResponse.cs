namespace GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;

public sealed record CustomerWorkOrderInventoryLineResponse(
    Guid Id,
    Guid EstimateId,
    Guid InventoryItemId,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);
