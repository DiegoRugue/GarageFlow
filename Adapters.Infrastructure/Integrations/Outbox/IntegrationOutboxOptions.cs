namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed class IntegrationOutboxOptions
{
    public const string SectionName = "Integrations:Outbox";

    public bool Enabled { get; init; }
    public int BatchSize { get; init; } = 10;
    public int PollingIntervalSeconds { get; init; } = 5;
    public int LeaseDurationSeconds { get; init; } = 300;
    public int InitialRetryDelaySeconds { get; init; } = 5;
    public int MaxRetryDelaySeconds { get; init; } = 300;

    internal bool IsValid() =>
        BatchSize > 0
        && PollingIntervalSeconds > 0
        && LeaseDurationSeconds > 0
        && InitialRetryDelaySeconds > 0
        && MaxRetryDelaySeconds >= InitialRetryDelaySeconds;
}
