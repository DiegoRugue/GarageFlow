using System.Text.Json;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Domain.WorkOrders.Events;
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Application.WorkOrders.Integrations;

public sealed class WorkOrderIntegrationOutboxMapper : IIntegrationOutboxMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

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
            JsonSerializer.Serialize(integrationEvent, SerializerOptions),
            integrationEvent.OccurredAt,
            correlationId);
    }
}
