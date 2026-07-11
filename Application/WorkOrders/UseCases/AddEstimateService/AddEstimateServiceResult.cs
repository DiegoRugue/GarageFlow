namespace GarageFlow.Application.WorkOrders.UseCases.AddEstimateService;

public sealed record AddEstimateServiceResult(
    Guid EstimateId,
    Guid ServiceId,
    string Description,
    decimal UnitPrice,
    decimal TotalPrice);
