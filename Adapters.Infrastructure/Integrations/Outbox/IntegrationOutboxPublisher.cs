using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed class IntegrationOutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IOptions<IntegrationOutboxOptions> options,
    IntegrationOutboxTelemetry telemetry) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly IntegrationOutboxOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly IntegrationOutboxTelemetry _telemetry =
        telemetry ?? throw new ArgumentNullException(nameof(telemetry));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        await PollSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollingIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PollSafelyAsync(stoppingToken);
        }
    }

    private async Task PollSafelyAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IIntegrationOutboxProcessor>();
        try
        {
            await processor.ProcessBatchAsync(cancellationToken);
            _telemetry.RecordSuccessfulPoll();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _telemetry.RecordFailedPoll();
        }
    }
}
