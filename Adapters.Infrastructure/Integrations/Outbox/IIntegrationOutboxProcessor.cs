namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public interface IIntegrationOutboxProcessor
{
    Task<IntegrationOutboxProcessingResult> ProcessBatchAsync(
        CancellationToken cancellationToken = default);
}
