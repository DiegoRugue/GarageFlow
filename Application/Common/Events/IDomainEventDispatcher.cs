using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Application.Common.Events;

public interface IDomainEventDispatcher
{
    ValueTask DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken);
}
