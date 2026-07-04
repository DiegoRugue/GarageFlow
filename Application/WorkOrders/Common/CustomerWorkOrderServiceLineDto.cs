namespace GarageFlow.Application.WorkOrders.Common;

public sealed record CustomerWorkOrderServiceLineDto(
    Guid Id,
    Guid EstimateId,
    Guid ServiceId,
    string Description,
    decimal UnitPrice,
    decimal TotalPrice,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt);
