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
        private readonly TransactionBehavior<GarageFlowCommand, string> _behavior;

        public PipelineFixture(bool hasMappedMessages, FailureBoundary? failure = null)
        {
            _failure = failure;
            Operations = [];
            var unitOfWork = new RecordingUnitOfWork(Operations, failure);
            Mapper = new RecordingMapper(Operations, hasMappedMessages, failure);
            var writer = new RecordingWriter(Operations, failure);
            var dispatcher = new RecordingDispatcher(Operations, failure);
            _behavior = new TransactionBehavior<GarageFlowCommand, string>(
                unitOfWork,
                dispatcher,
                Mapper,
                writer);
        }

        public List<string> Operations { get; }
        public RecordingMapper Mapper { get; }

        public async Task<string> HandleAsync(GarageFlowCommand command)
        {
            MessageHandlerDelegate<GarageFlowCommand, string> next = (_, _) =>
            {
                Operations.Add("Handler");
                ThrowIf(FailureBoundary.Handler, _failure);
                return new ValueTask<string>("handled");
            };

            return await _behavior.Handle(command, next, CancellationToken.None);
        }
    }

    private sealed class RecordingUnitOfWork(List<string> operations, FailureBoundary? failure) : IUnitOfWork
    {
        private int _saveCount;

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            operations.Add("Begin");
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            operations.Add("Commit");
            ThrowIf(FailureBoundary.Commit, failure);
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            operations.Add("Rollback");
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _saveCount++;
            var boundary = _saveCount == 1 ? FailureBoundary.Save1 : FailureBoundary.Save2;
            operations.Add(boundary.ToString());
            ThrowIf(boundary, failure);
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
        FailureBoundary? failure) : IIntegrationOutboxMapper
    {
        public string? CorrelationId { get; private set; }

        public IReadOnlyList<IntegrationOutboxMessage> Map(
            IReadOnlyCollection<DomainEvent> domainEvents,
            string? correlationId)
        {
            operations.Add("Map");
            CorrelationId = correlationId;
            ThrowIf(FailureBoundary.Map, failure);

            return hasMappedMessages
                ? [new IntegrationOutboxMessage(Guid.NewGuid(), "test.v1", Guid.NewGuid(), "{}", DateTime.UtcNow, correlationId)]
                : [];
        }
    }

    private sealed class RecordingWriter(List<string> operations, FailureBoundary? failure) : IOutboxWriter
    {
        public Task WriteAsync(
            IReadOnlyCollection<IntegrationOutboxMessage> messages,
            CancellationToken cancellationToken)
        {
            operations.Add("Write");
            ThrowIf(FailureBoundary.Write, failure);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDispatcher(List<string> operations, FailureBoundary? failure) : IDomainEventDispatcher
    {
        public ValueTask DispatchAsync(
            IReadOnlyCollection<DomainEvent> domainEvents,
            CancellationToken cancellationToken)
        {
            operations.Add("Dispatch");
            ThrowIf(FailureBoundary.Dispatch, failure);
            return ValueTask.CompletedTask;
        }
    }

    private static void ThrowIf(FailureBoundary expected, FailureBoundary? actual)
    {
        if (expected == actual)
        {
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
