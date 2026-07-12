using GarageFlow.Application.Common.Messaging;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Application.WorkOrders.UseCases.ReceiveEstimateDecision;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Tests.Shared.InventoryItems;
using GarageFlow.Tests.Shared.WorkOrders;
using Moq;

namespace GarageFlow.Tests.Unit.WorkOrders;

public sealed class ReceiveEstimateDecisionHandlerTests
{
    private const string ValidPayloadHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    public static TheoryData<string> InvalidPayloadHashes => new()
    {
        ValidPayloadHash.ToUpperInvariant(),
        $"{ValidPayloadHash[..63]}g",
        ValidPayloadHash[..63],
        $"{ValidPayloadHash}0"
    };

    [Fact]
    public void Command_UsesEventIdAsDFormatCorrelationId()
    {
        var fixture = new Fixture();
        var command = fixture.ValidCommand();

        var correlated = Assert.IsAssignableFrom<ICorrelatedCommand>(command);

        Assert.Equal(command.EventId.ToString("D"), correlated.CorrelationId);
    }

    [Fact]
    public async Task Handle_NewApproval_UsesCanonicalDecisionAndReturnsNonDuplicate()
    {
        var fixture = new Fixture();

        var result = await fixture.HandleAsync(fixture.ValidCommand(decision: "aPpRoVeD"));

        Assert.False(result.IsDuplicate);
        Assert.Equal(EstimateStatus.Approved, fixture.Estimate.Status);
        Assert.Equal(WorkOrderStatus.InProgress, fixture.WorkOrder.Status);
        fixture.InventoryItemRepository.Verify(repository => repository.GetByIdForStockReservationAsync(
            It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NewRejection_UsesCanonicalDecisionAndRestoresReservedStock()
    {
        var inventoryItem = new InventoryItemBuilder().WithStockQuantity(5).Build();
        var fixture = new Fixture(inventoryItem);

        var result = await fixture.HandleAsync(fixture.ValidCommand(decision: "rEjEcTeD"));

        Assert.False(result.IsDuplicate);
        Assert.Equal(EstimateStatus.Rejected, fixture.Estimate.Status);
        Assert.Equal(WorkOrderStatus.Diagnosing, fixture.WorkOrder.Status);
        Assert.Equal(7, inventoryItem.StockQuantity.Value);
    }

    [Fact]
    public async Task Handle_RegistersInboxWithOccurredAtAndReceivedAtFromTimeProvider()
    {
        var fixture = new Fixture();
        var command = fixture.ValidCommand();

        await fixture.HandleAsync(command);

        fixture.Inbox.Verify(inbox => inbox.RegisterAsync(
            command.EventId,
            command.PayloadHash,
            command.OccurredAt,
            fixture.ReceivedAt,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SameHashDuplicate_ReturnsDuplicateBeforeLoadingOrProcessingWorkOrder()
    {
        var fixture = new Fixture(isNew: false);

        var result = await fixture.HandleAsync(fixture.ValidCommand(decision: "Rejected"));

        Assert.True(result.IsDuplicate);
        fixture.WorkOrderRepository.Verify(repository => repository.GetByIdForEstimateMutationAsync(
            It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.InventoryItemRepository.Verify(repository => repository.GetByIdForStockReservationAsync(
            It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(EstimateStatus.Pending, fixture.Estimate.Status);
    }

    [Fact]
    public async Task Handle_DifferentHashDuplicate_ThrowsConflictBeforeLoadingWorkOrder()
    {
        var fixture = new Fixture(isNew: false, storedPayloadHash: new string('f', 64));
        var command = fixture.ValidCommand();

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => fixture.HandleAsync(command));

        Assert.Equal(
            $"Event ID '{command.EventId}' was already used with a different payload.",
            exception.Message);
        fixture.WorkOrderRepository.Verify(repository => repository.GetByIdForEstimateMutationAsync(
            It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("EventId")]
    [InlineData("WorkOrderId")]
    [InlineData("EstimateId")]
    public async Task Handle_EmptyIdentifier_RejectsBeforeRegisteringInbox(string identifier)
    {
        var fixture = new Fixture();
        var command = fixture.ValidCommand();
        command = identifier switch
        {
            "EventId" => command with { EventId = Guid.Empty },
            "WorkOrderId" => command with { WorkOrderId = Guid.Empty },
            "EstimateId" => command with { EstimateId = Guid.Empty },
            _ => throw new InvalidOperationException($"Unknown identifier '{identifier}'.")
        };

        await Assert.ThrowsAsync<ValidationException>(() => fixture.HandleAsync(command));

        fixture.VerifyInboxWasNotCalled();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Pending")]
    [InlineData(" Approved ")]
    public async Task Handle_InvalidDecision_RejectsBeforeRegisteringInbox(string decision)
    {
        var fixture = new Fixture();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => fixture.HandleAsync(fixture.ValidCommand(decision: decision)));

        Assert.Equal("Decision must be either 'Approved' or 'Rejected'.", exception.Message);
        fixture.VerifyInboxWasNotCalled();
    }

    [Theory]
    [MemberData(nameof(InvalidPayloadHashes))]
    public async Task Handle_InvalidPayloadHash_RejectsBeforeRegisteringInbox(string payloadHash)
    {
        var fixture = new Fixture();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => fixture.HandleAsync(fixture.ValidCommand(payloadHash: payloadHash)));

        Assert.Equal(
            "Payload hash must be a 64-character lowercase hexadecimal SHA-256 hash.",
            exception.Message);
        fixture.VerifyInboxWasNotCalled();
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task Handle_NonUtcOccurredAt_RejectsBeforeRegisteringInbox(DateTimeKind kind)
    {
        var fixture = new Fixture();
        var occurredAt = DateTime.SpecifyKind(fixture.OccurredAt, kind);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => fixture.HandleAsync(fixture.ValidCommand(occurredAt: occurredAt)));

        Assert.Equal("Occurred-at timestamp must be UTC.", exception.Message);
        fixture.VerifyInboxWasNotCalled();
    }

    [Fact]
    public async Task Handle_MissingWorkOrder_ThrowsExistingNotFoundMessageAfterInboxRegistration()
    {
        var fixture = new Fixture(workOrderExists: false);
        var command = fixture.ValidCommand();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => fixture.HandleAsync(command));

        Assert.Equal($"Work order with ID '{command.WorkOrderId}' was not found.", exception.Message);
        fixture.Inbox.Verify(inbox => inbox.RegisterAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PropagatesCancellationTokenThroughInboxRepositoryAndRejectionProcessor()
    {
        var inventoryItem = new InventoryItemBuilder().Build();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var fixture = new Fixture(inventoryItem, expectedCancellationToken: cancellationToken);

        await fixture.HandleAsync(fixture.ValidCommand(decision: "Rejected"), cancellationToken);

        fixture.Inbox.Verify(inbox => inbox.RegisterAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), cancellationToken),
            Times.Once);
        fixture.WorkOrderRepository.Verify(repository => repository.GetByIdForEstimateMutationAsync(
            It.IsAny<WorkOrderId>(), cancellationToken), Times.Once);
        fixture.InventoryItemRepository.Verify(repository => repository.GetByIdForStockReservationAsync(
            It.IsAny<InventoryItemId>(), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProcessingFails_PropagatesAfterRegistrationSoTransactionCanRollBackInbox()
    {
        var inventoryItem = new InventoryItemBuilder().Build();
        var fixture = new Fixture(inventoryItem, inventoryItemExists: false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.HandleAsync(fixture.ValidCommand(decision: "Rejected")));

        fixture.Inbox.Verify(inbox => inbox.RegisterAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.WorkOrderRepository.Verify(repository => repository.GetByIdForEstimateMutationAsync(
            It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Fixture
    {
        public DateTime OccurredAt { get; } = new(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc);
        public DateTime ReceivedAt { get; } = new(2026, 7, 12, 12, 1, 0, DateTimeKind.Utc);
        public WorkOrder WorkOrder { get; }
        public Estimate Estimate => WorkOrder.Estimates.Single();
        public Mock<IEstimateDecisionInbox> Inbox { get; } = new(MockBehavior.Strict);
        public Mock<IWorkOrderRepository> WorkOrderRepository { get; } = new(MockBehavior.Strict);
        public Mock<IInventoryItemRepository> InventoryItemRepository { get; } = new(MockBehavior.Strict);

        private readonly ReceiveEstimateDecisionHandler _handler;

        public Fixture(
            InventoryItem? inventoryItem = null,
            bool isNew = true,
            string storedPayloadHash = ValidPayloadHash,
            bool workOrderExists = true,
            bool inventoryItemExists = true,
            CancellationToken expectedCancellationToken = default)
        {
            WorkOrder = BuildPendingWorkOrder(inventoryItem);
            Inbox.Setup(inbox => inbox.RegisterAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    expectedCancellationToken))
                .ReturnsAsync(new EstimateDecisionInboxRegistration(isNew, storedPayloadHash));
            WorkOrderRepository.Setup(repository => repository.GetByIdForEstimateMutationAsync(
                    WorkOrder.Id,
                    expectedCancellationToken))
                .ReturnsAsync(workOrderExists ? WorkOrder : null);

            if (inventoryItem is not null)
            {
                InventoryItemRepository.Setup(repository => repository.GetByIdForStockReservationAsync(
                        inventoryItem.Id,
                        expectedCancellationToken))
                    .ReturnsAsync(inventoryItemExists ? inventoryItem : null);
            }

            var processor = new EstimateDecisionProcessor(InventoryItemRepository.Object);
            _handler = new ReceiveEstimateDecisionHandler(
                Inbox.Object,
                WorkOrderRepository.Object,
                processor,
                new FixedTimeProvider(ReceivedAt));
        }

        public ReceiveEstimateDecisionCommand ValidCommand(
            string decision = "Approved",
            DateTime? occurredAt = null,
            string payloadHash = ValidPayloadHash) => new(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                WorkOrder.Id.Value,
                Estimate.Id.Value,
                decision,
                occurredAt ?? OccurredAt,
                payloadHash);

        public async Task<ReceiveEstimateDecisionResult> HandleAsync(
            ReceiveEstimateDecisionCommand command,
            CancellationToken cancellationToken = default) =>
            await _handler.Handle(command, cancellationToken);

        public void VerifyInboxWasNotCalled() => Inbox.Verify(inbox => inbox.RegisterAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);

        private static WorkOrder BuildPendingWorkOrder(InventoryItem? inventoryItem)
        {
            var workOrder = new WorkOrderBuilder().BuildCreated();
            var estimate = workOrder.CreateEstimate();
            WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
            if (inventoryItem is not null)
            {
                workOrder.AddInventoryLine(
                    estimate.Id,
                    inventoryItem.Id,
                    inventoryItem.Description,
                    EstimateItemQuantity.Create(2),
                    inventoryItem.Cost,
                    inventoryItem.Price);
            }

            workOrder.SubmitEstimate(estimate.Id);
            return workOrder;
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
