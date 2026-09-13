namespace GarageFlow.Application.WorkOrders.ReadModels;

public sealed record WorkOrderDailyMetricsReadModel(long CreatedCount, long CompletedCount, double? AverageDurationSeconds);
