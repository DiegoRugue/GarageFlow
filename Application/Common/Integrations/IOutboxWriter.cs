namespace GarageFlow.Application.Common.Integrations;

public interface IOutboxWriter
{
    Task WriteAsync(
        IReadOnlyCollection<IntegrationOutboxMessage> messages,
        CancellationToken cancellationToken);
}
