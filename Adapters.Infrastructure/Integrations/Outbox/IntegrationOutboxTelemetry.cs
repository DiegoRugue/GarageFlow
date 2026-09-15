using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed class IntegrationOutboxTelemetry : IDisposable
{
    public const string MeterName = "GarageFlow.Integrations";
    private const string OutcomeTagName = "outbox.outcome";
    private const string FailureKindTagName = "outbox.failure.kind";

    private static readonly Action<ILogger, string, string, string, string?, Exception?> LogResult =
        LoggerMessage.Define<string, string, string, string?>(
            LogLevel.Information,
            new EventId(1, "OutboxResult"),
            "{EventName}: outcome={Outcome}, failureKind={FailureKind}, correlationId={CorrelationId}");
    private static readonly Action<ILogger, string, string, string, string?, Exception?> LogPollFailure =
        LoggerMessage.Define<string, string, string, string?>(
            LogLevel.Error,
            new EventId(2, "OutboxPollFailure"),
            "{EventName}: outcome={Outcome}, failureKind={FailureKind}, correlationId={CorrelationId}");

    private readonly ILogger<IntegrationOutboxTelemetry> _logger;
    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _results;
    private readonly Counter<long> _polls;

    public IntegrationOutboxTelemetry(ILogger<IntegrationOutboxTelemetry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _results = _meter.CreateCounter<long>("garageflow.integration.outbox.results", "{result}");
        _polls = _meter.CreateCounter<long>("garageflow.integration.outbox.polls", "{poll}");
    }

    internal void RecordResult(OutboxOutcome outcome, OutboxFailureKind failureKind, string? correlationId)
    {
        var outcomeValue = Value(outcome);
        var failureValue = Value(failureKind);
        _results.Add(1,
            new KeyValuePair<string, object?>(OutcomeTagName, outcomeValue),
            new KeyValuePair<string, object?>(FailureKindTagName, failureValue));
        LogResult(_logger, "OutboxResult", outcomeValue, failureValue, SafeCorrelationId(correlationId), null);
    }

    public void RecordSuccessfulPoll()
    {
        EmitResultZeros();
        _polls.Add(0, new KeyValuePair<string, object?>(OutcomeTagName, "failure"));
        _polls.Add(1, new KeyValuePair<string, object?>(OutcomeTagName, "success"));
    }

    public void RecordFailedPoll()
    {
        _polls.Add(1, new KeyValuePair<string, object?>(OutcomeTagName, "failure"));
        LogPollFailure(_logger, "OutboxPollFailure", "failure", "poll_failed", null, null);
    }

    private void EmitResultZeros()
    {
        AddZero("processed", "none");
        foreach (var failure in new[] { "unsupported_event", "invalid_payload", "publish_failed", "timeout" })
        {
            AddZero("rescheduled", failure);
            AddZero("ownership_lost", failure);
        }
        AddZero("ownership_lost", "none");
    }

    private void AddZero(string outcome, string failureKind) =>
        _results.Add(0,
            new KeyValuePair<string, object?>(OutcomeTagName, outcome),
            new KeyValuePair<string, object?>(FailureKindTagName, failureKind));

    private static string? SafeCorrelationId(string? value) =>
        value is { Length: 32 }
        && value.All(Uri.IsHexDigit)
        && value.Any(character => character != '0')
            ? value.ToLowerInvariant()
            : null;

    private static string Value(OutboxOutcome value) => value switch
    {
        OutboxOutcome.Processed => "processed",
        OutboxOutcome.Rescheduled => "rescheduled",
        OutboxOutcome.OwnershipLost => "ownership_lost",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Value(OutboxFailureKind value) => value switch
    {
        OutboxFailureKind.None => "none",
        OutboxFailureKind.UnsupportedEvent => "unsupported_event",
        OutboxFailureKind.InvalidPayload => "invalid_payload",
        OutboxFailureKind.PublishFailed => "publish_failed",
        OutboxFailureKind.Timeout => "timeout",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public void Dispose() => _meter.Dispose();
}

internal enum OutboxOutcome { Processed, Rescheduled, OwnershipLost }
internal enum OutboxFailureKind { None, UnsupportedEvent, InvalidPayload, PublishFailed, Timeout }
