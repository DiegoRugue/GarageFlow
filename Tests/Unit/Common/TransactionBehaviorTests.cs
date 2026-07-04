using GarageFlow.Application.Common.Behaviors;
using GarageFlow.Application.Common.Events;
using GarageFlow.Application.Common.Messaging;
using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.SharedKernel.Persistence;
using Mediator;
using Moq;

namespace GarageFlow.Tests.Unit.Common;

public class TransactionBehaviorTests
{
    [Fact]
    public async Task Handle_ShouldDequeueDomainEventsBeforeCommitAndDispatchAfterCommit_WhenCommandSucceeds()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var dispatcherMock = new Mock<IDomainEventDispatcher>(MockBehavior.Strict);
        var domainEvent = new TestDomainEvent();
        var domainEvents = new List<DomainEvent> { domainEvent };
        var sequence = new MockSequence();

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.DequeueDomainEvents())
            .Returns(domainEvents);

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        dispatcherMock
            .InSequence(sequence)
            .Setup(x => x.DispatchAsync(domainEvents, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var behavior = new TransactionBehavior<TestCommand, string>(unitOfWorkMock.Object, dispatcherMock.Object);
        var command = new TestCommand();
        MessageHandlerDelegate<TestCommand, string> next = static (_, _) => new ValueTask<string>("handled");

        var result = await behavior.Handle(command, next, CancellationToken.None);

        Assert.Equal("handled", result);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.VerifyAll();
        dispatcherMock.VerifyAll();
    }

    [Fact]
    public async Task Handle_ShouldRollbackAndSkipDispatch_WhenCommandFails()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var dispatcherMock = new Mock<IDomainEventDispatcher>();
        var expectedException = new InvalidOperationException("boom");
        var sequence = new MockSequence();

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var behavior = new TransactionBehavior<TestCommand, string>(unitOfWorkMock.Object, dispatcherMock.Object);
        var command = new TestCommand();
        MessageHandlerDelegate<TestCommand, string> next = (_, _) => throw expectedException;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await behavior.Handle(command, next, CancellationToken.None));

        Assert.Same(expectedException, exception);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.DequeueDomainEvents(), Times.Never);
        dispatcherMock.Verify(
            x => x.DispatchAsync(It.IsAny<IReadOnlyCollection<DomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotRollback_WhenDispatchFailsAfterCommit()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var dispatcherMock = new Mock<IDomainEventDispatcher>();
        var domainEvents = new List<DomainEvent> { new TestDomainEvent() };
        var expectedException = new InvalidOperationException("Dispatch failed.");

        unitOfWorkMock
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.DequeueDomainEvents())
            .Returns(domainEvents);

        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        dispatcherMock
            .Setup(x => x.DispatchAsync(domainEvents, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var behavior = new TransactionBehavior<TestCommand, string>(unitOfWorkMock.Object, dispatcherMock.Object);
        MessageHandlerDelegate<TestCommand, string> next = static (_, _) => new ValueTask<string>("handled");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await behavior.Handle(new TestCommand(), next, CancellationToken.None));

        Assert.Same(expectedException, exception);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldDispatchEventsCapturedBeforeCommit_WhenDeletedTrackedEntityEventsWouldDisappearAfterCommit()
    {
        var domainEvent = new TestDomainEvent();
        var unitOfWork = new DeletedEntityEventUnitOfWork(domainEvent);
        IReadOnlyCollection<DomainEvent>? dispatchedEvents = null;
        var dispatcherMock = new Mock<IDomainEventDispatcher>();

        dispatcherMock
            .Setup(x => x.DispatchAsync(It.IsAny<IReadOnlyCollection<DomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken _) => dispatchedEvents = domainEvents)
            .Returns(ValueTask.CompletedTask);

        var behavior = new TransactionBehavior<TestCommand, string>(unitOfWork, dispatcherMock.Object);
        MessageHandlerDelegate<TestCommand, string> next = static (_, _) => new ValueTask<string>("handled");

        var result = await behavior.Handle(new TestCommand(), next, CancellationToken.None);

        Assert.Equal("handled", result);
        Assert.True(unitOfWork.DequeuedBeforeCommit);
        Assert.True(unitOfWork.Committed);
        Assert.NotNull(dispatchedEvents);
        var dispatchedEvent = Assert.Single(dispatchedEvents);
        Assert.Same(domainEvent, dispatchedEvent);
    }

    private sealed record TestCommand : GarageFlow.Application.Common.Messaging.ICommand<string>;

    private sealed record TestDomainEvent : DomainEvent;

    private sealed class DeletedEntityEventUnitOfWork(DomainEvent domainEvent) : IUnitOfWork
    {
        public bool Committed { get; private set; }
        public bool DequeuedBeforeCommit { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public IReadOnlyList<DomainEvent> DequeueDomainEvents()
        {
            if (Committed)
            {
                return [];
            }

            DequeuedBeforeCommit = true;
            return [domainEvent];
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }
}
