namespace GarageFlow.Application.WorkOrders.AddEstimateService;

public sealed record AddEstimateServiceResult(
    Guid EstimateId,
    Guid ServiceId,
    string Description,
    decimal UnitPrice,
    decimal TotalPrice);
