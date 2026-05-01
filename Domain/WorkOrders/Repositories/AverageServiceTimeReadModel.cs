namespace GarageFlow.Domain.WorkOrders.Repositories;

public sealed record AverageServiceTimeReadModel(
    int CompletedWorkOrdersCount,
    double? AverageDurationMinutes);
