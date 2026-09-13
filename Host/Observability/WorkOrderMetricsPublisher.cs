using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Globalization;
using GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderDailyMetrics;
using Mediator;

namespace GarageFlow.Host.Observability;

public sealed class WorkOrderMetricsPublisher : BackgroundService
{
    internal const string MeterName = "GarageFlow.WorkOrders";
    private const string ReportingTimeZone = "America/Sao_Paulo";
    private const int ReportingDays = 7;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshBudget = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumSnapshotAge = TimeSpan.FromMinutes(10);
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById(ReportingTimeZone);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly Meter _meter = new(MeterName);
    private Snapshot? _snapshot;

    public WorkOrderMetricsPublisher(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _meter.CreateObservableGauge("garageflow.work_orders.created", () => ObserveCounts(row => row.CreatedCount), "{work_order}");
        _meter.CreateObservableGauge("garageflow.work_orders.completed", () => ObserveCounts(row => row.CompletedCount), "{work_order}");
        _meter.CreateObservableGauge("garageflow.work_orders.duration.mean", ObserveDurations, "s");
        _meter.CreateObservableGauge("garageflow.work_orders.snapshot.timestamp", ObserveTimestamps, "s");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RefreshAsync(stoppingToken);
                await Task.Delay(RefreshInterval, _timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown, including cancellation of an in-flight query.
        }
    }

    private async Task RefreshAsync(CancellationToken stoppingToken)
    {
        using var budget = new CancellationTokenSource(RefreshBudget, _timeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, budget.Token);
        try
        {
            var startedAt = _timeProvider.GetUtcNow();
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startedAt, BusinessTimeZone).DateTime);
            var rows = ImmutableArray.CreateBuilder<DailySnapshot>(ReportingDays);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            for (var offset = 0; offset < ReportingDays; offset++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var date = today.AddDays(-offset);
                var result = await mediator.Send(new GetWorkOrderDailyMetricsQuery(date), cancellation.Token);
                rows.Add(new(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), result.CreatedCount,
                    result.CompletedCount, result.AverageDurationSeconds));
            }
            cancellation.Token.ThrowIfCancellationRequested();
            // The window and exported timestamp share one start instant; a slow pre-midnight refresh
            // must not outrank a newer window from another replica. Publish only after all queries succeed.
            var snapshot = new Snapshot(rows.MoveToImmutable(), startedAt.ToUnixTimeSeconds(), _timeProvider.GetTimestamp());
            Volatile.Write(ref _snapshot, snapshot);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Retain the previous snapshot and its original age. Retry next period.
            // Query exceptions can contain SQL or customer data and must never enter telemetry.
        }
    }

    private Snapshot? FreshSnapshot()
    {
        var snapshot = Volatile.Read(ref _snapshot);
        return snapshot is not null && _timeProvider.GetElapsedTime(snapshot.CompletedTimestamp) <= MaximumSnapshotAge
            ? snapshot : null;
    }

    private IEnumerable<Measurement<long>> ObserveCounts(Func<DailySnapshot, long> value)
    {
        var snapshot = FreshSnapshot();
        if (snapshot is null) yield break;
        foreach (var row in snapshot.Days)
            yield return new(value(row), Tags(row));
    }

    private IEnumerable<Measurement<double>> ObserveDurations()
    {
        var snapshot = FreshSnapshot();
        if (snapshot is null) yield break;
        foreach (var row in snapshot.Days)
            if (row.AverageDurationSeconds is { } duration)
                yield return new(duration, Tags(row));
    }

    private IEnumerable<Measurement<long>> ObserveTimestamps()
    {
        var snapshot = FreshSnapshot();
        if (snapshot is null) yield break;
        foreach (var row in snapshot.Days)
            yield return new(snapshot.StartedUnixSeconds, Tags(row));
    }

    private static KeyValuePair<string, object?>[] Tags(DailySnapshot row) =>
        [new("work_orders.date", row.Date), new("work_orders.timezone", ReportingTimeZone)];

    public override void Dispose()
    {
        _meter.Dispose();
        base.Dispose();
    }

    private sealed record DailySnapshot(string Date, long CreatedCount, long CompletedCount, double? AverageDurationSeconds);
    private sealed record Snapshot(ImmutableArray<DailySnapshot> Days, long StartedUnixSeconds, long CompletedTimestamp);
}
