namespace GarageFlow.Domain.WorkOrders.Repositories;

public sealed record AverageServiceTimeReadModel(
    int CompletedServicesCount,
    double? AverageDurationMinutes);
