using GarageFlow.Application.WorkOrders.Common;
using System.Text.Json;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Application.WorkOrders.Ports;
using Microsoft.Extensions.Options;

namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed class IntegrationOutboxProcessor(
    IIntegrationOutboxRepository repository,
    IWorkOrderStatusNotificationPublisher? publisher,
    IOptions<IntegrationOutboxOptions> options,
    TimeProvider timeProvider,
    IntegrationOutboxTelemetry telemetry,
    IExecutionCommandPublisher? executionPublisher = null,
    IBillingChargePublisher? billingPublisher = null) : IIntegrationOutboxProcessor
{
    private readonly IIntegrationOutboxRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IWorkOrderStatusNotificationPublisher? _publisher = publisher;
    private static readonly string[] ExecutionEventKeys =
    [
        ExecutionSchedulingContract.EventKey, ExecutionSchedulingContract.StartKey, ExecutionSchedulingContract.CompleteKey
    ];
    private static readonly string[] BillingEventKeys = [BillingChargeContract.EventKey];

    private readonly IExecutionCommandPublisher? _executionPublisher = executionPublisher;
    private readonly IBillingChargePublisher? _billingPublisher = billingPublisher;
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
        var executionKeys = _executionPublisher is null ? [] : ExecutionEventKeys;
        var billingKeys = _billingPublisher is null ? [] : BillingEventKeys;
        var claims = await _repository.ClaimBatchAsync(
            leaseId,
            now,
            now.AddSeconds(_options.LeaseDurationSeconds),
            _options.BatchSize,
            includeEventKeys: _publisher is null ? [.. executionKeys, .. billingKeys] : null,
            excludeEventKeys: _publisher is null ? null : [
                .. (_executionPublisher is null ? ExecutionEventKeys : []),
                .. (_billingPublisher is null ? BillingEventKeys : [])
            ],
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
        if (ExecutionEventKeys.Contains(claim.EventKey) && _executionPublisher is not null)
        {
            return await PublishAsync(claim, token => _executionPublisher.PublishAsync(claim.Payload, token), cancellationToken);
        }

        if (claim.EventKey == BillingChargeContract.EventKey && _billingPublisher is not null)
        {
            return await PublishAsync(claim, token => _billingPublisher.PublishAsync(claim.Payload, token), cancellationToken);
        }

        if (claim.EventKey != WorkOrderStatusChangedIntegrationEvent.EventKey || _publisher is null)
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

        return await PublishAsync(claim, token => _publisher.PublishAsync(notification, token), cancellationToken);
    }

    private async Task<CompletedProcessing> PublishAsync(
        ClaimedIntegrationOutboxMessage claim,
        Func<CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        try
        {
            await publish(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return await RescheduleAsync(
                claim,
                claim.EventKey != WorkOrderStatusChangedIntegrationEvent.EventKey
                    ? $"Publisher failure ({exception.GetType().Name})."
                    : $"Publisher failure ({exception.GetType().Name}): {exception.Message}",
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
