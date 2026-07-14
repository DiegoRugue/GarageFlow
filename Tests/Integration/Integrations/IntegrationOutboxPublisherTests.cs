using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        IntegrationOutboxOptions options) =>
        new(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            NullLogger<IntegrationOutboxPublisher>.Instance);

    private sealed class RecordingProcessor(PollRecorder recorder) : IIntegrationOutboxProcessor
    {
        public Task<IntegrationOutboxProcessingResult> ProcessBatchAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref recorder.ProcessCalls) >= 2)
            {
                recorder.SecondPollObserved.TrySetResult();
            }

            return Task.FromResult(new IntegrationOutboxProcessingResult(0, 0, 0, 0));
        }
    }

    private sealed class PollRecorder
    {
        public int CreatedScopes;
        public int ProcessCalls;
        public TaskCompletionSource SecondPollObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
