namespace GarageFlow.Application.WorkOrders.GetAverageServiceTime;

public sealed record GetAverageServiceTimeResult(
    DateTime From,
    DateTime To,
    Guid? ServiceId,
    int CompletedServicesCount,
    double? AverageDurationMinutes);
