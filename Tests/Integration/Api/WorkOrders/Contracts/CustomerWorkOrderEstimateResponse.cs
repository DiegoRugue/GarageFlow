namespace GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;

public sealed record CustomerWorkOrderEstimateResponse(
    Guid Id,
    Guid WorkOrderId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CustomerWorkOrderInventoryLineResponse> InventoryLines,
    IReadOnlyList<CustomerWorkOrderServiceLineResponse> ServiceLines);
