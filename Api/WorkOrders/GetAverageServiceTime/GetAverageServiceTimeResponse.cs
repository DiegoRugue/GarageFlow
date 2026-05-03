namespace GarageFlow.Api.WorkOrders.GetAverageServiceTime;

public sealed record GetAverageServiceTimeResponse(
    DateTime From,
    DateTime To,
    Guid? ServiceId,
    int CompletedServicesCount,
    double? AverageDurationMinutes);
