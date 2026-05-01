namespace GarageFlow.Application.WorkOrders.GetAverageServiceTime;

public sealed record GetAverageServiceTimeResult(
    DateTime From,
    DateTime To,
    int CompletedWorkOrdersCount,
    double? AverageDurationMinutes);
