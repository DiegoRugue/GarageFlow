namespace GarageFlow.Adapters.Api.WorkOrders.AddEstimateService;

public sealed record AddEstimateServiceResponse(
    Guid EstimateId,
    Guid ServiceId,
    string Description,
    decimal UnitPrice,
    decimal TotalPrice);
