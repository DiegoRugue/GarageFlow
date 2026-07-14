using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Application.Common.Integrations;

public interface IIntegrationOutboxMapper
{
    IReadOnlyList<IntegrationOutboxMessage> Map(
        IReadOnlyCollection<DomainEvent> domainEvents,
        string? correlationId);
}
