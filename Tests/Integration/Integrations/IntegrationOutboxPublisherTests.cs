using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace GarageFlow.Tests.Integration.Integrations;

public sealed class IntegrationOutboxPublisherTests
{
    private static readonly TimeSpan PollObservationTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void Options_ShouldExposeExactDefaultsAndRejectInvalidValues()
    {
        var defaults = new IntegrationOutboxOptions();
        Assert.False(defaults.Enabled);
        Assert.Equal(10, defaults.BatchSize);
        Assert.Equal(5, defaults.PollingIntervalSeconds);
        Assert.Equal(300, defaults.LeaseDurationSeconds);
        Assert.Equal(5, defaults.InitialRetryDelaySeconds);
        Assert.Equal(300, defaults.MaxRetryDelaySeconds);

        var validator = new IntegrationOutboxOptionsValidator();
        Assert.True(validator.Validate(null, defaults).Succeeded);
        Assert.True(validator.Validate(null, new IntegrationOutboxOptions { BatchSize = 0 }).Failed);
        Assert.True(validator.Validate(null, new IntegrationOutboxOptions { PollingIntervalSeconds = 0 }).Failed);
        Assert.True(validator.Validate(null, new IntegrationOutboxOptions { LeaseDurationSeconds = 0 }).Failed);
        Assert.True(validator.Validate(null, new IntegrationOutboxOptions { InitialRetryDelaySeconds = 0 }).Failed);
        Assert.True(validator.Validate(null, new IntegrationOutboxOptions { InitialRetryDelaySeconds = 10, MaxRetryDelaySeconds = 5 }).Failed);
    }

    [Fact]
    public async Task Publisher_ShouldStayDisabledByDefault()
    {
        var recorder = new PollRecorder();
        await using var services = BuildServices(recorder);
        var publisher = CreatePublisher(services, new IntegrationOutboxOptions());

        await publisher.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await publisher.StopAsync(CancellationToken.None);

        Assert.Equal(0, recorder.CreatedScopes);
        Assert.Equal(0, recorder.ProcessCalls);
    }

    [Fact]
    public async Task Publisher_ShouldCreateFreshScopePerPollAndHonorCancellationWithoutHotLoop()
    {
        var recorder = new PollRecorder();
        await using var services = BuildServices(recorder);
        var publisher = CreatePublisher(services, new IntegrationOutboxOptions
        {
            Enabled = true,
            PollingIntervalSeconds = 1
        });

        await publisher.StartAsync(CancellationToken.None);
        await recorder.SecondPollObserved.Task.WaitAsync(PollObservationTimeout);
        await publisher.StopAsync(CancellationToken.None);
        var callsAfterStop = recorder.ProcessCalls;
        await Task.Delay(200);

        Assert.Equal(recorder.CreatedScopes, recorder.ProcessCalls);
        Assert.True(callsAfterStop >= 2);
        Assert.Equal(callsAfterStop, recorder.ProcessCalls);
    }

    [Fact]
    public async Task Publisher_ShouldEmitSuccessZerosAndControlledPollFailure()
    {
        var recorder = new PollRecorder { FailureOnCall = 2 };
        await using var services = BuildServices(recorder);
        var logs = new RecordingLogger<IntegrationOutboxTelemetry>();
        using var telemetry = new IntegrationOutboxTelemetry(logs);
        var measurements = new ConcurrentQueue<(string Name, string? Unit, long Value, Dictionary<string, string?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == IntegrationOutboxTelemetry.MeterName) current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var values = new Dictionary<string, string?>();
            foreach (var tag in tags) values[tag.Key] = tag.Value?.ToString();
            measurements.Enqueue((instrument.Name, instrument.Unit, value, values));
        });
        listener.Start();
        var publisher = CreatePublisher(services, new IntegrationOutboxOptions { Enabled = true, PollingIntervalSeconds = 1 }, telemetry);

        await publisher.StartAsync(CancellationToken.None);
        await recorder.SecondPollObserved.Task.WaitAsync(PollObservationTimeout);
        await publisher.StopAsync(CancellationToken.None);

        Assert.Contains(measurements, item => item.Name == "garageflow.integration.outbox.polls" && item.Value == 1 && item.Tags["outbox.outcome"] == "success");
        Assert.Contains(measurements, item => item.Name == "garageflow.integration.outbox.polls" && item.Value == 1 && item.Tags["outbox.outcome"] == "failure");
        Assert.Contains(measurements, item => item.Name == "garageflow.integration.outbox.polls" && item.Unit == "{poll}" && item.Value == 0 && item.Tags["outbox.outcome"] == "failure");
        Assert.Contains(measurements, item => item.Name == "garageflow.integration.outbox.results" && item.Value == 0 && item.Tags["outbox.failure.kind"] == "publish_failed");
        Assert.Contains(measurements, item => item.Name == "garageflow.integration.outbox.results" && item.Unit == "{result}");
        var failure = Assert.Single(logs.Entries);
        Assert.Equal("OutboxPollFailure", failure["EventName"]);
        Assert.Equal("poll_failed", failure["FailureKind"]);
    }

    private static ServiceProvider BuildServices(PollRecorder recorder)
    {
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddScoped<IIntegrationOutboxProcessor>(provider =>
        {
            Interlocked.Increment(ref provider.GetRequiredService<PollRecorder>().CreatedScopes);
            return new RecordingProcessor(provider.GetRequiredService<PollRecorder>());
        });
        return services.BuildServiceProvider();
    }

    private static IntegrationOutboxPublisher CreatePublisher(
        ServiceProvider services,
        IntegrationOutboxOptions options,
        IntegrationOutboxTelemetry? telemetry = null) =>
        new(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            telemetry ?? new IntegrationOutboxTelemetry(NullLogger<IntegrationOutboxTelemetry>.Instance));

    private sealed class RecordingProcessor(PollRecorder recorder) : IIntegrationOutboxProcessor
    {
        public Task<IntegrationOutboxProcessingResult> ProcessBatchAsync(CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref recorder.ProcessCalls);
            if (call >= 2)
            {
                recorder.SecondPollObserved.TrySetResult();
            }

            if (call == recorder.FailureOnCall) throw new InvalidOperationException("private-marker");

            return Task.FromResult(new IntegrationOutboxProcessingResult(0, 0, 0, 0));
        }
    }

    private sealed class PollRecorder
    {
        public int CreatedScopes;
        public int ProcessCalls;
        public int FailureOnCall;
        public TaskCompletionSource SecondPollObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class RecordingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<Dictionary<string, string?>> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(x => x.Key, x => x.Value?.ToString()));
    }
}
