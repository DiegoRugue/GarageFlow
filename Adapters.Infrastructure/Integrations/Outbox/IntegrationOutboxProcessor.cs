using System.Text.Json;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Application.WorkOrders.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed class IntegrationOutboxProcessor(
    IIntegrationOutboxRepository repository,
    IWorkOrderStatusNotificationPublisher publisher,
    IOptions<IntegrationOutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<IntegrationOutboxProcessor> logger) : IIntegrationOutboxProcessor
{
    private static readonly Action<ILogger, Guid, ProcessingOutcome, Exception?> LogMessageOutcome =
        LoggerMessage.Define<Guid, ProcessingOutcome>(
            LogLevel.Information,
            new EventId(1, nameof(LogMessageOutcome)),
            "Integration outbox message {MessageId} completed with status {Status}.");

    private readonly IIntegrationOutboxRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IWorkOrderStatusNotificationPublisher _publisher =
        publisher ?? throw new ArgumentNullException(nameof(publisher));
    private readonly IntegrationOutboxOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ILogger<IntegrationOutboxProcessor> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<IntegrationOutboxProcessingResult> ProcessBatchAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var leaseId = Guid.NewGuid();
        var claims = await _repository.ClaimBatchAsync(
            leaseId,
            now,
            now.AddSeconds(_options.LeaseDurationSeconds),
            _options.BatchSize,
            cancellationToken);

        var processed = 0;
        var rescheduled = 0;
        var ownershipLost = 0;
        foreach (var claim in claims)
        {
            var outcome = await ProcessClaimAsync(claim, cancellationToken);
            switch (outcome)
            {
                case ProcessingOutcome.Processed:
                    processed++;
                    break;
                case ProcessingOutcome.Rescheduled:
                    rescheduled++;
                    break;
                case ProcessingOutcome.OwnershipLost:
                    ownershipLost++;
                    break;
            }
        }

        return new IntegrationOutboxProcessingResult(claims.Count, processed, rescheduled, ownershipLost);
    }

    private async Task<ProcessingOutcome> ProcessClaimAsync(
        ClaimedIntegrationOutboxMessage claim,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                claim.EventKey,
                WorkOrderStatusChangedIntegrationEvent.EventKey,
                StringComparison.Ordinal))
        {
            return await RescheduleAsync(
                claim,
                $"Unsupported integration event key '{claim.EventKey}'.",
                cancellationToken);
        }

        WorkOrderStatusChangedIntegrationEvent notification;
        try
        {
            notification = IntegrationEventJson.Deserialize<WorkOrderStatusChangedIntegrationEvent>(claim.Payload)
                ?? throw new JsonException("The integration event payload deserialized to null.");
        }
        catch (JsonException)
        {
            return await RescheduleAsync(
                claim,
                $"Malformed integration event payload for '{claim.EventKey}'.",
                cancellationToken);
        }

        if (!IsValid(notification, claim.AggregateId))
        {
            return await RescheduleAsync(
                claim,
                $"Invalid integration event payload for '{claim.EventKey}'.",
                cancellationToken);
        }

        try
        {
            await _publisher.PublishAsync(notification, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return await RescheduleAsync(
                claim,
                $"Publisher failure ({exception.GetType().Name}): {exception.Message}",
                cancellationToken);
        }

        var marked = await _repository.MarkProcessedAsync(
            claim.Id,
            claim.LeaseId,
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        var outcome = marked ? ProcessingOutcome.Processed : ProcessingOutcome.OwnershipLost;
        LogOutcome(claim.Id, outcome);
        return outcome;
    }

    private async Task<ProcessingOutcome> RescheduleAsync(
        ClaimedIntegrationOutboxMessage claim,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        var delay = OutboxRetrySchedule.ForAttempt(
            claim.AttemptCount,
            TimeSpan.FromSeconds(_options.InitialRetryDelaySeconds),
            TimeSpan.FromSeconds(_options.MaxRetryDelaySeconds));
        var boundedDiagnostic = diagnostic.Length <= IntegrationOutboxRepository.MaximumErrorLength
            ? diagnostic
            : diagnostic[..IntegrationOutboxRepository.MaximumErrorLength];
        var updated = await _repository.RescheduleAsync(
            claim.Id,
            claim.LeaseId,
            _timeProvider.GetUtcNow().UtcDateTime.Add(delay),
            boundedDiagnostic,
            cancellationToken);
        var outcome = updated ? ProcessingOutcome.Rescheduled : ProcessingOutcome.OwnershipLost;
        LogOutcome(claim.Id, outcome);
        return outcome;
    }

    private static bool IsValid(
        WorkOrderStatusChangedIntegrationEvent notification,
        Guid aggregateId) =>
        notification.WorkOrderId != Guid.Empty
        && notification.WorkOrderId == aggregateId
        && !string.IsNullOrWhiteSpace(notification.PreviousStatus)
        && !string.IsNullOrWhiteSpace(notification.CurrentStatus)
        && notification.OccurredAt.Kind == DateTimeKind.Utc;

    private void LogOutcome(Guid messageId, ProcessingOutcome outcome) =>
        LogMessageOutcome(_logger, messageId, outcome, null);

    private enum ProcessingOutcome
    {
        Processed,
        Rescheduled,
        OwnershipLost
    }
}
