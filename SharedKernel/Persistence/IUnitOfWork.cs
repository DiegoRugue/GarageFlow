using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.SharedKernel.Persistence;

public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<DomainEvent> DequeueDomainEvents();
}
