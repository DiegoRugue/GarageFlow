using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Application.Common.Integrations;

namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed class EfOutboxWriter(GarageFlowDbContext dbContext) : IOutboxWriter
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task WriteAsync(
        IReadOnlyCollection<IntegrationOutboxMessage> messages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();

        var entities = messages.Select(message => IntegrationOutboxMessageEntity.Create(
            message.Id,
            message.EventKey,
            message.AggregateId,
            message.Payload,
            message.OccurredAt,
            message.CorrelationId));

        _dbContext.IntegrationOutboxMessages.AddRange(entities);
        return Task.CompletedTask;
    }
}
