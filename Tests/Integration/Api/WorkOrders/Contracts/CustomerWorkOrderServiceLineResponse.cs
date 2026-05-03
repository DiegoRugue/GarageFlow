namespace GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;

public sealed record CustomerWorkOrderServiceLineResponse(
    Guid Id,
    Guid EstimateId,
    Guid ServiceId,
    string Description,
    decimal UnitPrice,
    decimal TotalPrice,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt);
