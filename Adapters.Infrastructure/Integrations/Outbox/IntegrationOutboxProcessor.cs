using System.Text.Json;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Application.WorkOrders.Ports;
using Microsoft.Extensions.Options;

namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed class IntegrationOutboxProcessor(
    IIntegrationOutboxRepository repository,
    IWorkOrderStatusNotificationPublisher publisher,
    IOptions<IntegrationOutboxOptions> options,
    TimeProvider timeProvider,
    IntegrationOutboxTelemetry telemetry) : IIntegrationOutboxProcessor
{
    private readonly IIntegrationOutboxRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IWorkOrderStatusNotificationPublisher _publisher =
        publisher ?? throw new ArgumentNullException(nameof(publisher));
    private readonly IntegrationOutboxOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly IntegrationOutboxTelemetry _telemetry =
        telemetry ?? throw new ArgumentNullException(nameof(telemetry));

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
            var completed = await ProcessClaimAsync(claim, cancellationToken);
            _telemetry.RecordResult(completed.Outcome, completed.FailureKind, claim.CorrelationId);
            switch (completed.Outcome)
            {
                case OutboxOutcome.Processed:
                    processed++;
                    break;
                case OutboxOutcome.Rescheduled:
                    rescheduled++;
                    break;
                case OutboxOutcome.OwnershipLost:
                    ownershipLost++;
                    break;
            }
        }

        return new IntegrationOutboxProcessingResult(claims.Count, processed, rescheduled, ownershipLost);
    }

    private async Task<CompletedProcessing> ProcessClaimAsync(
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
                OutboxFailureKind.UnsupportedEvent,
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
                OutboxFailureKind.InvalidPayload,
                cancellationToken);
        }

        if (!IsValid(notification, claim.AggregateId))
        {
            return await RescheduleAsync(
                claim,
                $"Invalid integration event payload for '{claim.EventKey}'.",
                OutboxFailureKind.InvalidPayload,
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
                exception is OperationCanceledException ? OutboxFailureKind.Timeout : OutboxFailureKind.PublishFailed,
                cancellationToken);
        }

        var marked = await _repository.MarkProcessedAsync(
            claim.Id,
            claim.LeaseId,
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        var outcome = marked ? OutboxOutcome.Processed : OutboxOutcome.OwnershipLost;
        return new(outcome, OutboxFailureKind.None);
    }

    private async Task<CompletedProcessing> RescheduleAsync(
        ClaimedIntegrationOutboxMessage claim,
        string diagnostic,
        OutboxFailureKind failureKind,
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
        var outcome = updated ? OutboxOutcome.Rescheduled : OutboxOutcome.OwnershipLost;
        return new(outcome, failureKind);
    }

    private static bool IsValid(
        WorkOrderStatusChangedIntegrationEvent notification,
        Guid aggregateId) =>
        notification.WorkOrderId != Guid.Empty
        && notification.WorkOrderId == aggregateId
        && !string.IsNullOrWhiteSpace(notification.PreviousStatus)
        && !string.IsNullOrWhiteSpace(notification.CurrentStatus)
        && notification.OccurredAt.Kind == DateTimeKind.Utc;

    private sealed record CompletedProcessing(OutboxOutcome Outcome, OutboxFailureKind FailureKind);
}
