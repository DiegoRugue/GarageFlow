namespace GarageFlow.Api.WorkOrders.Responses;

public sealed record WorkOrderServiceLineResponse(
    Guid Id,
    Guid EstimateId,
    Guid ServiceId,
    string Description,
    decimal UnitPrice,
    decimal TotalPrice,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt);
