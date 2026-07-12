using System.Diagnostics;
using GarageFlow.Application.Common.Behaviors;
using GarageFlow.Application.Common.Events;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Application.Common.Messaging;
using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.SharedKernel.Persistence;
using Mediator;
using GarageFlowCommand = GarageFlow.Application.Common.Messaging.ICommand<string>;

namespace GarageFlow.Tests.Unit.Common;

public sealed class TransactionBehaviorTests
{
    [Fact]
    public async Task Handle_ShouldExecuteEveryBoundaryInExactOrder_WhenMappedMessagesExist()
    {
        var fixture = new PipelineFixture(hasMappedMessages: true);

        var result = await fixture.HandleAsync(new TestCommand());

        Assert.Equal("handled", result);
        Assert.Equal(
            ["Begin", "Handler", "Save1", "Dequeue", "Map", "Write", "Save2", "Commit", "Dispatch"],
            fixture.Operations);
    }

    [Fact]
    public async Task Handle_ShouldSkipWriteAndSecondSave_WhenNoMessagesAreMapped()
    {
        var fixture = new PipelineFixture(hasMappedMessages: false);

        await fixture.HandleAsync(new TestCommand());

        Assert.Equal(
            ["Begin", "Handler", "Save1", "Dequeue", "Map", "Commit", "Dispatch"],
            fixture.Operations);
    }

    [Theory]
    [InlineData(FailureBoundary.Handler)]
    [InlineData(FailureBoundary.Save1)]
    [InlineData(FailureBoundary.Map)]
    [InlineData(FailureBoundary.Write)]
    [InlineData(FailureBoundary.Save2)]
    [InlineData(FailureBoundary.Commit)]
    public async Task Handle_ShouldRollbackAndNeverDispatch_WhenPreCommitBoundaryFails(FailureBoundary failure)
    {
        var fixture = new PipelineFixture(hasMappedMessages: true, failure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.HandleAsync(new TestCommand()));

        Assert.Equal($"{failure} failed.", exception.Message);
        Assert.Equal(ExpectedFailureOperations(failure), fixture.Operations);
    }

    [Theory]
    [InlineData(FailureBoundary.Handler)]
    [InlineData(FailureBoundary.Save2)]
    [InlineData(FailureBoundary.Commit)]
    public async Task Handle_ShouldCompleteRollbackWithNonCancelledToken_WhenRequestIsCancelledAtPreCommitBoundary(
        FailureBoundary failure)
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var originalException = new OperationCanceledException(
            $"{failure} cancelled.",
            cancellationSource.Token);
        var fixture = new PipelineFixture(
            hasMappedMessages: true,
            failure,
            originalException);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.HandleAsync(new TestCommand(), cancellationSource.Token));

        Assert.Same(originalException, exception);
        Assert.True(fixture.UnitOfWork.RollbackCompleted);
        Assert.False(fixture.UnitOfWork.RollbackCancellationToken.IsCancellationRequested);
        Assert.DoesNotContain("Dispatch", fixture.Operations);
    }

    [Fact]
    public async Task Handle_ShouldPreserveOriginalCancellation_WhenRollbackFails()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var originalException = new OperationCanceledException(
            "Handler cancelled.",
            cancellationSource.Token);
        var fixture = new PipelineFixture(
            hasMappedMessages: true,
            FailureBoundary.Handler,
            originalException,
            rollbackFails: true);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.HandleAsync(new TestCommand(), cancellationSource.Token));

        Assert.Same(originalException, exception);
        Assert.Contains(nameof(ThrowIf), exception.StackTrace);
        Assert.Equal(["Begin", "Handler", "Rollback"], fixture.Operations);
        Assert.False(fixture.UnitOfWork.RollbackCancellationToken.IsCancellationRequested);
        Assert.DoesNotContain("Dispatch", fixture.Operations);
    }

    [Fact]
    public async Task Handle_ShouldPropagateDispatchFailureWithoutRollback_WhenCommitSucceeded()
    {
        var fixture = new PipelineFixture(hasMappedMessages: true, FailureBoundary.Dispatch);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.HandleAsync(new TestCommand()));

        Assert.Equal("Dispatch failed.", exception.Message);
        Assert.Equal(
            ["Begin", "Handler", "Save1", "Dequeue", "Map", "Write", "Save2", "Commit", "Dispatch"],
            fixture.Operations);
        Assert.DoesNotContain("Rollback", fixture.Operations);
    }

    [Fact]
    public async Task Handle_ShouldPropagateCommandCorrelationIdToMapper_WhenCommandIsCorrelated()
    {
        var fixture = new PipelineFixture(hasMappedMessages: false);

        await fixture.HandleAsync(new CorrelatedTestCommand("correlation-123"));

        Assert.Equal("correlation-123", fixture.Mapper.CorrelationId);
    }

    [Fact]
    public async Task Handle_ShouldUseCurrentActivityTraceId_WhenCommandIsNotCorrelated()
    {
        using var activity = new Activity("transaction-test")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        var fixture = new PipelineFixture(hasMappedMessages: false);

        await fixture.HandleAsync(new TestCommand());

        Assert.Equal(activity.TraceId.ToHexString(), fixture.Mapper.CorrelationId);
    }

    private sealed class PipelineFixture
    {
        private readonly FailureBoundary? _failure;
        private readonly Exception? _failureException;
        private readonly TransactionBehavior<GarageFlowCommand, string> _behavior;

        public PipelineFixture(
            bool hasMappedMessages,
            FailureBoundary? failure = null,
            Exception? failureException = null,
            bool rollbackFails = false)
        {
            _failure = failure;
            _failureException = failureException;
            Operations = [];
            UnitOfWork = new RecordingUnitOfWork(
                Operations,
                failure,
                failureException,
                rollbackFails);
            Mapper = new RecordingMapper(Operations, hasMappedMessages, failure, failureException);
            var writer = new RecordingWriter(Operations, failure, failureException);
            var dispatcher = new RecordingDispatcher(Operations, failure, failureException);
            _behavior = new TransactionBehavior<GarageFlowCommand, string>(
                UnitOfWork,
                dispatcher,
                Mapper,
                writer);
        }

        public List<string> Operations { get; }
        public RecordingMapper Mapper { get; }
        public RecordingUnitOfWork UnitOfWork { get; }

        public async Task<string> HandleAsync(
            GarageFlowCommand command,
            CancellationToken cancellationToken = default)
        {
            MessageHandlerDelegate<GarageFlowCommand, string> next = (_, _) =>
            {
                Operations.Add("Handler");
                ThrowIf(FailureBoundary.Handler, _failure, _failureException);
                return new ValueTask<string>("handled");
            };

            return await _behavior.Handle(command, next, cancellationToken);
        }
    }

    internal sealed class RecordingUnitOfWork(
        List<string> operations,
        FailureBoundary? failure,
        Exception? failureException,
        bool rollbackFails) : IUnitOfWork
    {
        private int _saveCount;

        public CancellationToken RollbackCancellationToken { get; private set; }
        public bool RollbackCompleted { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            operations.Add("Begin");
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            operations.Add("Commit");
            ThrowIf(FailureBoundary.Commit, failure, failureException);
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            operations.Add("Rollback");
            RollbackCancellationToken = cancellationToken;
            if (rollbackFails)
            {
                throw new InvalidOperationException("Rollback failed.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            RollbackCompleted = true;
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _saveCount++;
            var boundary = _saveCount == 1 ? FailureBoundary.Save1 : FailureBoundary.Save2;
            operations.Add(boundary.ToString());
            ThrowIf(boundary, failure, failureException);
            return Task.FromResult(1);
        }

        public IReadOnlyList<DomainEvent> DequeueDomainEvents()
        {
            operations.Add("Dequeue");
            return [new TestDomainEvent()];
        }
    }

    internal sealed class RecordingMapper(
        List<string> operations,
        bool hasMappedMessages,
        FailureBoundary? failure,
        Exception? failureException) : IIntegrationOutboxMapper
    {
        public string? CorrelationId { get; private set; }

        public IReadOnlyList<IntegrationOutboxMessage> Map(
            IReadOnlyCollection<DomainEvent> domainEvents,
            string? correlationId)
        {
            operations.Add("Map");
            CorrelationId = correlationId;
            ThrowIf(FailureBoundary.Map, failure, failureException);

            return hasMappedMessages
                ? [new IntegrationOutboxMessage(Guid.NewGuid(), "test.v1", Guid.NewGuid(), "{}", DateTime.UtcNow, correlationId)]
                : [];
        }
    }

    private sealed class RecordingWriter(
        List<string> operations,
        FailureBoundary? failure,
        Exception? failureException) : IOutboxWriter
    {
        public Task WriteAsync(
            IReadOnlyCollection<IntegrationOutboxMessage> messages,
            CancellationToken cancellationToken)
        {
            operations.Add("Write");
            ThrowIf(FailureBoundary.Write, failure, failureException);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDispatcher(
        List<string> operations,
        FailureBoundary? failure,
        Exception? failureException) : IDomainEventDispatcher
    {
        public ValueTask DispatchAsync(
            IReadOnlyCollection<DomainEvent> domainEvents,
            CancellationToken cancellationToken)
        {
            operations.Add("Dispatch");
            ThrowIf(FailureBoundary.Dispatch, failure, failureException);
            return ValueTask.CompletedTask;
        }
    }

    private static void ThrowIf(
        FailureBoundary expected,
        FailureBoundary? actual,
        Exception? failureException = null)
    {
        if (expected == actual)
        {
            if (failureException is not null)
            {
                throw failureException;
            }

            throw new InvalidOperationException($"{expected} failed.");
        }
    }

    private static IReadOnlyList<string> ExpectedFailureOperations(FailureBoundary failure) => failure switch
    {
        FailureBoundary.Handler => ["Begin", "Handler", "Rollback"],
        FailureBoundary.Save1 => ["Begin", "Handler", "Save1", "Rollback"],
        FailureBoundary.Map => ["Begin", "Handler", "Save1", "Dequeue", "Map", "Rollback"],
        FailureBoundary.Write => ["Begin", "Handler", "Save1", "Dequeue", "Map", "Write", "Rollback"],
        FailureBoundary.Save2 => ["Begin", "Handler", "Save1", "Dequeue", "Map", "Write", "Save2", "Rollback"],
        FailureBoundary.Commit => ["Begin", "Handler", "Save1", "Dequeue", "Map", "Write", "Save2", "Commit", "Rollback"],
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
    };

    private sealed record TestCommand : GarageFlowCommand;
    private sealed record CorrelatedTestCommand(string CorrelationId) : GarageFlowCommand, ICorrelatedCommand;
    private sealed record TestDomainEvent : DomainEvent;

    public enum FailureBoundary
    {
        Handler,
        Save1,
        Map,
        Write,
        Save2,
        Commit,
        Dispatch
    }
}
