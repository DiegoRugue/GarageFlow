namespace GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;

public sealed class AverageServiceTimeResponse
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public Guid? ServiceId { get; init; }
    public int CompletedServicesCount { get; init; }
    public double? AverageDurationMinutes { get; init; }
}
