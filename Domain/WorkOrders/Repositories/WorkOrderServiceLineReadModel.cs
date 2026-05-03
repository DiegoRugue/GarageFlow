namespace GarageFlow.Domain.WorkOrders.Repositories;

public sealed record WorkOrderServiceLineReadModel(
    Guid Id,
    Guid EstimateId,
    Guid ServiceId,
    string DescriptionSnapshot,
    decimal UnitPrice,
    decimal TotalPrice,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt);
