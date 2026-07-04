namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderById;

public sealed record WorkOrderServiceLineDto(
    Guid Id,
    Guid EstimateId,
    Guid ServiceId,
    string Description,
    decimal UnitPrice,
    decimal TotalPrice,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt);
