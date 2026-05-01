namespace GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;

public sealed record WorkOrderServiceLineResponse(
    Guid Id,
    Guid EstimateId,
    Guid ServiceId,
    string Description,
    decimal UnitPrice,
    decimal TotalPrice);
