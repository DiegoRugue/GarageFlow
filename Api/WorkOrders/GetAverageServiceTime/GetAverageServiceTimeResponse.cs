namespace GarageFlow.Api.WorkOrders.GetAverageServiceTime;

public sealed record GetAverageServiceTimeResponse(
    DateTime From,
    DateTime To,
    int CompletedWorkOrdersCount,
    double? AverageDurationMinutes);
