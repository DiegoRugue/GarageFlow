namespace GarageFlow.Api.WorkOrders.Responses;

public sealed record CustomerWorkOrderServiceLineResponse(
    Guid Id,
    Guid EstimateId,
    Guid ServiceId,
    string Description,
    decimal UnitPrice,
    decimal TotalPrice);
