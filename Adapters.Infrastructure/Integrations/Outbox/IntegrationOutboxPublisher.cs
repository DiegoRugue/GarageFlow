using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed class IntegrationOutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IOptions<IntegrationOutboxOptions> options,
    ILogger<IntegrationOutboxPublisher> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, int, int, int, Exception?> LogPollResult =
        LoggerMessage.Define<int, int, int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogPollResult)),
            "Integration outbox poll claimed {ClaimedCount}, processed {ProcessedCount}, rescheduled {RescheduledCount}, and lost ownership of {OwnershipLostCount} messages.");
    private static readonly Action<ILogger, string, Exception?> LogPollFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2, nameof(LogPollFailure)),
            "Integration outbox poll failed with failure type {FailureType}.");

    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly IntegrationOutboxOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<IntegrationOutboxPublisher> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

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
            var result = await processor.ProcessBatchAsync(cancellationToken);
            LogPollResult(
                _logger,
                result.ClaimedCount,
                result.ProcessedCount,
                result.RescheduledCount,
                result.OwnershipLostCount,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogPollFailure(_logger, exception.GetType().Name, null);
        }
    }
}
