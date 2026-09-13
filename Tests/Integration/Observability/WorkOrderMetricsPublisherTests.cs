using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderDailyMetrics;
using GarageFlow.Host.Observability;
using GarageFlow.Tests.Integration.Support.Fixtures;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace GarageFlow.Tests.Integration.Observability;

[Collection(WebApplicationFactoryStartupTestGroup.Name)]
public sealed class WorkOrderMetricsPublisherTests
{
    private const string MeterName = "GarageFlow.WorkOrders";
    private const string PublisherName = "WorkOrderMetricsPublisher";
    private static readonly string[] AttributeNames = ["work_orders.date", "work_orders.timezone"];

    [Fact]
    public async Task DisabledObservabilityDoesNotRegisterPublisherOrAccessQueries()
    {
        await using var fixture = new PublisherFixture(enabled: false);
        Assert.Null(fixture.Publisher);
        Assert.Empty(fixture.Capture());
        Assert.Empty(fixture.Dates);
    }

    [Fact]
    public async Task StartupPublishesSevenSaoPauloDatesWithSafeGaugeContractAndNoQueriesInCallbacks()
    {
        await using var fixture = new PublisherFixture();
        await fixture.StartAsync();
        var points = fixture.Capture();
        Assert.Equal(7, fixture.Dates.Count);
        Assert.Equal(new DateOnly(2026, 9, 12), fixture.Dates.First()); // 01:00 UTC is prior business day.
        Assert.Equal(new DateOnly(2026, 9, 6), fixture.Dates.Last());
        Assert.Equal(27, points.Length); // Seven counts/timestamps, six available means.
        Assert.All(points, point =>
        {
            Assert.Equal(AttributeNames, point.Tags.Keys.Order().ToArray());
            Assert.Equal("America/Sao_Paulo", point.Tags["work_orders.timezone"]);
        });
        Assert.All(points.Where(p => p.Name.EndsWith("created", StringComparison.Ordinal) || p.Name.EndsWith("completed", StringComparison.Ordinal)), p => Assert.Equal("{work_order}", p.Unit));
        Assert.All(points.Where(p => p.Name.EndsWith("mean", StringComparison.Ordinal) || p.Name.EndsWith("timestamp", StringComparison.Ordinal)), p => Assert.Equal("s", p.Unit));
        Assert.Equal(12, Point(points, "garageflow.work_orders.created", "2026-09-12").Value);
        Assert.Equal(2, Point(points, "garageflow.work_orders.completed", "2026-09-12").Value);
        Assert.Equal(1200, Point(points, "garageflow.work_orders.duration.mean", "2026-09-12").Value);
        Assert.Equal(0, Point(points, "garageflow.work_orders.duration.mean", "2026-09-11").Value);
        Assert.DoesNotContain(points, p => p.Name == "garageflow.work_orders.duration.mean" && p.Tags["work_orders.date"] == "2026-09-10");
        Assert.Equal(fixture.Clock.GetUtcNow().ToUnixTimeSeconds(), Point(points, "garageflow.work_orders.snapshot.timestamp", "2026-09-12").Value);
        fixture.Capture();
        Assert.Equal(7, fixture.Dates.Count);
    }

    [Fact]
    public async Task SnapshotTimestampRecordsSuccessfulRefreshStartAndDoesNotChangeOnObservation()
    {
        await using var fixture = new PublisherFixture();
        fixture.QueryElapsed = TimeSpan.FromSeconds(2);
        var before = fixture.Clock.GetUtcNow();
        await fixture.StartAsync();
        var expected = before.ToUnixTimeSeconds();
        Assert.Equal(expected, Point(fixture.Capture(), "garageflow.work_orders.snapshot.timestamp", "2026-09-12").Value);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(expected, Point(fixture.Capture(), "garageflow.work_orders.snapshot.timestamp", "2026-09-12").Value);
    }

    [Fact]
    public async Task RefreshCrossingMidnightKeepsStartWindowAndSortsBeforeNewerWindow()
    {
        await using var fixture = new PublisherFixture();
        fixture.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(55));
        fixture.QueryElapsed = TimeSpan.FromSeconds(2);
        var startedAt = fixture.Clock.GetUtcNow(); // 02:59:55 UTC, still September12 in Sao Paulo.
        var newerWindowStartedAt = startedAt.AddSeconds(6); // Another replica starts after midnight.
        await fixture.StartAsync(); // Seven queries finish at03:00:09 UTC.
        Assert.True(fixture.Clock.GetUtcNow() > newerWindowStartedAt);
        var points = fixture.Capture();
        Assert.Contains(points, p => p.Tags["work_orders.date"] == "2026-09-06");
        Assert.DoesNotContain(points, p => p.Tags["work_orders.date"] == "2026-09-13");
        Assert.All(points.Where(p => p.Name == "garageflow.work_orders.snapshot.timestamp"), p =>
        {
            Assert.Equal(startedAt.ToUnixTimeSeconds(), p.Value);
            Assert.True(p.Value < newerWindowStartedAt.ToUnixTimeSeconds());
        });
    }
    [Fact]
    public async Task NextPeriodReplacesEntireWindowAndUsesRefreshStartTime()
    {
        await using var fixture = new PublisherFixture();
        await fixture.StartAsync();
        fixture.Clock.Advance(TimeSpan.FromDays(1));
        await fixture.WaitForRefreshAsync(14);
        var points = fixture.Capture();
        Assert.DoesNotContain(points, p => p.Tags["work_orders.date"] == "2026-09-06");
        Assert.Contains(points, p => p.Tags["work_orders.date"] == "2026-09-13");
        Assert.All(points.Where(p => p.Name.EndsWith("timestamp", StringComparison.Ordinal)), p => Assert.Equal(fixture.Clock.GetUtcNow().ToUnixTimeSeconds(), p.Value));
    }

    [Fact]
    public async Task FailedRefreshKeepsPreviousSnapshotWithoutExtendingFreshnessThenSuppressesIt()
    {
        await using var fixture = new PublisherFixture();
        await fixture.StartAsync();
        var originalTimestamp = Point(fixture.Capture(), "garageflow.work_orders.snapshot.timestamp", "2026-09-12").Value;
        fixture.Fail = true;
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        await fixture.WaitForRefreshAsync(8);
        Assert.Equal(originalTimestamp, Point(fixture.Capture(), "garageflow.work_orders.snapshot.timestamp", "2026-09-12").Value);
        fixture.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        await fixture.WaitForRefreshAsync(9);
        Assert.Empty(fixture.Capture());
        fixture.Fail = false;
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        await fixture.WaitForRefreshAsync(16);
        Assert.NotEmpty(fixture.Capture());
    }

    [Fact]
    public async Task PartialRefreshIsNeverPublishedAndBudgetCancelsAllQueriesWithinThirtySeconds()
    {
        await using var fixture = new PublisherFixture();
        fixture.BlockOnCall = 3;
        fixture.QueryElapsed = TimeSpan.FromSeconds(10); // First two queries consume 20 seconds of the same budget.
        await fixture.StartUntilBlockedAsync();
        Assert.Empty(fixture.Capture());
        fixture.Clock.Advance(TimeSpan.FromSeconds(9));
        Assert.False(fixture.BlockedToken.IsCancellationRequested);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        await fixture.WaitForPollingAsync();
        Assert.True(fixture.BlockedToken.IsCancellationRequested);
        Assert.Empty(fixture.Capture());
        Assert.Equal(3, fixture.Dates.Count);
    }

    [Fact]
    public async Task ShutdownCancelsInFlightQueryAndDoesNotPublishPartialResults()
    {
        await using var fixture = new PublisherFixture();
        fixture.BlockOnCall = 1;
        await fixture.StartUntilBlockedAsync();
        await fixture.Publisher!.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(fixture.BlockedToken.IsCancellationRequested);
        Assert.Empty(fixture.Capture());
        Assert.True(fixture.Publisher.ExecuteTask!.IsCompletedSuccessfully);
    }

    private static CapturedPoint Point(CapturedPoint[] points, string name, string date) =>
        Assert.Single(points, p => p.Name == name && p.Tags["work_orders.date"] == date);

    private sealed record CapturedPoint(string Name, string? Unit, double Value, Dictionary<string, string> Tags);

    private sealed class PublisherFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly MeterListener _listener = new();
        private readonly List<CapturedPoint> _points = [];
        private readonly TaskCompletionSource _blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualMetricsTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 9, 13, 1, 0, 0, TimeSpan.Zero));
        public ConcurrentQueue<DateOnly> Dates { get; } = new();
        public BackgroundService? Publisher { get; }
        public bool Fail { get; set; }
        public TimeSpan QueryElapsed { get; set; }
        public int BlockOnCall { get; set; }
        public CancellationToken BlockedToken { get; private set; }

        public PublisherFixture(bool enabled = true)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Observability:Enabled"] = enabled.ToString(),
                ["Observability:OtlpEndpoint"] = "http://127.0.0.1:1",
                ["Observability:Environment"] = "integration"
            });
            builder.Services.AddSingleton<TimeProvider>(Clock);
            var mediator = new Mock<IMediator>(MockBehavior.Strict);
            mediator.Setup(m => m.Send(It.IsAny<IRequest<GetWorkOrderDailyMetricsResult>>(), It.IsAny<CancellationToken>()))
                .Returns((IRequest<GetWorkOrderDailyMetricsResult> request, CancellationToken token) => QueryAsync((GetWorkOrderDailyMetricsQuery)request, token));
            builder.Services.AddScoped<IMediator>(_ => mediator.Object);
            builder.AddGarageFlowObservability();
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MeterName) listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Capture(instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Capture(instrument, value, tags));
            _listener.Start();
            _services = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Publisher = _services.GetServices<IHostedService>().OfType<BackgroundService>().SingleOrDefault(s => s.GetType().Name == PublisherName);
        }

        private async ValueTask<GetWorkOrderDailyMetricsResult> QueryAsync(GetWorkOrderDailyMetricsQuery query, CancellationToken token)
        {
            Dates.Enqueue(query.Date);
            if (Fail) throw new InvalidOperationException("customer-private-marker");
            if (Dates.Count == BlockOnCall)
            {
                BlockedToken = token;
                _blocked.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            Clock.Advance(QueryElapsed);
            var from = query.Date.ToDateTime(new TimeOnly(3, 0), DateTimeKind.Utc);
            return new(query.Date, "America/Sao_Paulo", from, from.AddDays(1), 12, 2,
                query.Date.Day == 10 ? null : query.Date.Day == 11 ? 0 : 1200);
        }

        public async Task StartAsync()
        {
            Assert.NotNull(Publisher);
            await Publisher.StartAsync(CancellationToken.None);
            await WaitForRefreshAsync(7);
        }
        public async Task StartUntilBlockedAsync()
        {
            Assert.NotNull(Publisher);
            await Publisher.StartAsync(CancellationToken.None);
            await _blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        public async Task WaitForRefreshAsync(int calls)
        {
            await WaitUntilAsync(() => Dates.Count >= calls);
            await WaitForPollingAsync();
        }
        public Task WaitForPollingAsync() => WaitUntilAsync(() => Clock.HasTimer(TimeSpan.FromMinutes(5)));
        public CapturedPoint[] Capture()
        {
            _points.Clear();
            _listener.RecordObservableInstruments();
            return _points.ToArray();
        }
        private void Capture(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var attributes = new Dictionary<string, string>();
            foreach (var tag in tags) attributes.Add(tag.Key, tag.Value?.ToString() ?? "");
            _points.Add(new(instrument.Name, instrument.Unit, value, attributes));
        }
        public async ValueTask DisposeAsync()
        {
            if (Publisher is not null) await Publisher.StopAsync(CancellationToken.None);
            _listener.Dispose();
            await _services.DisposeAsync();
        }
        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!condition()) await Task.Delay(10, timeout.Token);
        }
    }
}
