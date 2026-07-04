namespace GarageFlow.Application.WorkOrders.ReadModels;

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
