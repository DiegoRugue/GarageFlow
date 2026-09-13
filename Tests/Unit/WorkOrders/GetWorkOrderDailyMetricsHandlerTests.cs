using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Application.WorkOrders.ReadModels;
using GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderDailyMetrics;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Moq;

namespace GarageFlow.Tests.Unit.WorkOrders;

public sealed class GetWorkOrderDailyMetricsHandlerTests
{
    [Theory]
    [InlineData(2026, 9, 13, 3)]
    [InlineData(2019, 1, 15, 2)]
    public async Task Handle_ShouldUseSaoPauloUtcBoundsAndPreserveMetrics(int year, int month, int day, int utcHour)
    {
        var date = new DateOnly(year, month, day);
        var from = new DateTime(year, month, day, utcHour, 0, 0, DateTimeKind.Utc);
        using var cancellation = new CancellationTokenSource();
        var queries = new Mock<IWorkOrderMetricsQueries>(MockBehavior.Strict);
        queries.Setup(query => query.GetDailyAsync(from, from.AddDays(1), cancellation.Token))
            .ReturnsAsync(new WorkOrderDailyMetricsReadModel(7, 2, 1200));
        var handler = new GetWorkOrderDailyMetricsHandler(queries.Object);

        var result = await handler.Handle(new GetWorkOrderDailyMetricsQuery(date), cancellation.Token);

        Assert.Equal(new GetWorkOrderDailyMetricsResult(date, "America/Sao_Paulo", from, from.AddDays(1), 7, 2, 1200), result);
        Assert.Equal(DateTimeKind.Utc, result.FromUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, result.ToUtc.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    public async Task Handle_ShouldPreserveAbsentAndZeroDuration(double? duration)
    {
        var queries = new Mock<IWorkOrderMetricsQueries>();
        queries.Setup(query => query.GetDailyAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkOrderDailyMetricsReadModel(0, duration.HasValue ? 1 : 0, duration));

        var result = await new GetWorkOrderDailyMetricsHandler(queries.Object)
            .Handle(new GetWorkOrderDailyMetricsQuery(new DateOnly(2026, 9, 13)), CancellationToken.None);

        Assert.Equal(duration, result.AverageDurationSeconds);
        Assert.Equal(duration.HasValue ? 1 : 0, result.CompletedCount);
    }

    [Theory]
    [InlineData(9999, 12, 31)]
    [InlineData(2018, 11, 4)]
    [InlineData(2018, 11, 3)]
    public async Task Handle_ShouldRejectUnrepresentableOrInvalidMidnightBounds(int year, int month, int day)
    {
        var queries = new Mock<IWorkOrderMetricsQueries>(MockBehavior.Strict);
        var handler = new GetWorkOrderDailyMetricsHandler(queries.Object);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new GetWorkOrderDailyMetricsQuery(new DateOnly(year, month, day)), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Handle_ShouldPropagateQueryCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var queries = new Mock<IWorkOrderMetricsQueries>();
        queries.Setup(query => query.GetDailyAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() => new GetWorkOrderDailyMetricsHandler(queries.Object)
            .Handle(new GetWorkOrderDailyMetricsQuery(new DateOnly(2026, 9, 13)), cancellation.Token).AsTask());
    }
}
