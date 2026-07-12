using GarageFlow.Application.Common.Integrations;
using GarageFlow.Domain.WorkOrders.Events;
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Application.WorkOrders.Integrations;

public sealed class WorkOrderIntegrationOutboxMapper : IIntegrationOutboxMapper
{
    public IReadOnlyList<IntegrationOutboxMessage> Map(
        IReadOnlyCollection<DomainEvent> domainEvents,
        string? correlationId)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        return domainEvents
            .OfType<WorkOrderStatusChanged>()
            .Select(domainEvent => Map(domainEvent, correlationId))
            .ToList();
    }

    private static IntegrationOutboxMessage Map(
        WorkOrderStatusChanged domainEvent,
        string? correlationId)
    {
        var integrationEvent = new WorkOrderStatusChangedIntegrationEvent(
            domainEvent.WorkOrderId.Value,
            domainEvent.PreviousStatus.ToString(),
            domainEvent.NewStatus.ToString(),
            domainEvent.UpdatedAt);

        return new IntegrationOutboxMessage(
            Guid.NewGuid(),
            WorkOrderStatusChangedIntegrationEvent.EventKey,
            integrationEvent.WorkOrderId,
            IntegrationEventJson.Serialize(integrationEvent),
            integrationEvent.OccurredAt,
            correlationId);
    }
}
