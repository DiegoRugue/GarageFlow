namespace GarageFlow.Application.WorkOrders;

public sealed record CustomerWorkOrderServiceLineDto(
    Guid Id,
    Guid EstimateId,
    Guid ServiceId,
    string Description,
    decimal UnitPrice,
    decimal TotalPrice);
