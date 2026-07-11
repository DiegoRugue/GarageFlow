namespace GarageFlow.Application.WorkOrders.ReadModels;

public sealed record AverageServiceTimeReadModel(
    int CompletedServicesCount,
    double? AverageDurationMinutes);
