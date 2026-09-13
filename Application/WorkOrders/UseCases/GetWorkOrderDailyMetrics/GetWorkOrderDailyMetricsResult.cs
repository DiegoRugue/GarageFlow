namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderDailyMetrics;

public sealed record GetWorkOrderDailyMetricsResult(
    DateOnly Date,
    string TimeZone,
    DateTime FromUtc,
    DateTime ToUtc,
    long CreatedCount,
    long CompletedCount,
    double? AverageDurationSeconds);
