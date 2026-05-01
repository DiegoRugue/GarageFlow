namespace GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;

public sealed record AverageServiceTimeResponse(
    DateTime From,
    DateTime To,
    int CompletedWorkOrdersCount,
    double? AverageDurationMinutes);
