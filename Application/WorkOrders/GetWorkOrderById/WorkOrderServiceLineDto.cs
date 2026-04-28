namespace GarageFlow.Application.WorkOrders.GetWorkOrderById;

public sealed record WorkOrderServiceLineDto(
    Guid Id,
    Guid EstimateId,
    Guid ServiceId,
    string Description,
    decimal UnitPrice,
    decimal TotalPrice);
