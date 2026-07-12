using System.Diagnostics;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.Events;
using GarageFlow.Tests.Shared.WorkOrders;

namespace GarageFlow.Tests.Unit.WorkOrders;

public sealed class WorkOrderLifecycleTests
{
    [Theory]
    [InlineData(WorkOrderStatus.Received, 1)]
    [InlineData(WorkOrderStatus.Diagnosing, 2)]
    [InlineData(WorkOrderStatus.WaitingApproval, 3)]
    [InlineData(WorkOrderStatus.InProgress, 5)]
    [InlineData(WorkOrderStatus.Completed, 6)]
    [InlineData(WorkOrderStatus.Delivered, 7)]
    [InlineData(WorkOrderStatus.Cancelled, 8)]
    public void WorkOrderStatus_ShouldPreserveNumericContract(WorkOrderStatus status, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)status);
    }

    [Fact]
    public void WorkOrderStatus_ShouldLeaveLegacyValueFourUndefined()
    {
        Assert.False(Enum.IsDefined(typeof(WorkOrderStatus), 4));
    }

    [Fact]
    public void Create_ShouldStartInReceived()
    {
        var workOrder = WorkOrder.Create(CustomerId.New(), VehicleId.New());

        Assert.Equal("Received", workOrder.Status.ToString());
        var created = Assert.Single(workOrder.DomainEvents.OfType<WorkOrderCreated>());
        Assert.Equal("Received", created.Status.ToString());
    }

    [Fact]
    public void StartDiagnosis_ShouldTransitionFromReceivedToDiagnosing()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();

        AssertStatusChange(workOrder, workOrder.StartDiagnosis, (WorkOrderStatus)1, WorkOrderStatus.Diagnosing);
    }

    [Fact]
    public void SubmitEstimate_ShouldTransitionFromDiagnosingToWaitingApproval()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.StartDiagnosis();

        AssertStatusChange(
            workOrder,
            () => workOrder.SubmitEstimate(estimate.Id),
            WorkOrderStatus.Diagnosing,
            WorkOrderStatus.WaitingApproval);
    }

    [Fact]
    public void FirstEstimateSubmission_ShouldAdvanceReceivedThroughDiagnosingToWaitingApproval()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.ClearDomainEvents();

        workOrder.SubmitEstimate(estimate.Id);

        Assert.Collection(
            workOrder.DomainEvents.OfType<WorkOrderStatusChanged>(),
            changed => AssertStatusChanged(changed, (WorkOrderStatus)1, WorkOrderStatus.Diagnosing),
            changed => AssertStatusChanged(changed, WorkOrderStatus.Diagnosing, WorkOrderStatus.WaitingApproval));
        Assert.True(workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Last().UpdatedAt <= workOrder.UpdatedAt);
    }

    [Fact]
    public void ApproveEstimate_ShouldTransitionFromWaitingApprovalToInProgress_AndSetStartedAt()
    {
        var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();
        var estimate = Assert.Single(workOrder.Estimates);
        var beforeApproval = DateTime.UtcNow;

        var statusChanged = AssertStatusChange(
            workOrder,
            () => workOrder.ApproveEstimate(estimate.Id),
            WorkOrderStatus.WaitingApproval,
            WorkOrderStatus.InProgress);

        var approved = Assert.Single(workOrder.DomainEvents.OfType<EstimateApproved>());
        Assert.NotNull(workOrder.StartedAt);
        Assert.Equal(workOrder.StartedAt.Value, workOrder.UpdatedAt);
        Assert.Equal(workOrder.StartedAt.Value, statusChanged.UpdatedAt);
        Assert.Equal(workOrder.StartedAt.Value, approved.ApprovedAt);
        Assert.InRange(workOrder.StartedAt.Value, beforeApproval, DateTime.UtcNow);
    }

    [Fact]
    public void RejectEstimate_ShouldTransitionFromWaitingApprovalBackToDiagnosing()
    {
        var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();
        var estimate = Assert.Single(workOrder.Estimates);

        AssertStatusChange(
            workOrder,
            () => workOrder.RejectEstimate(estimate.Id),
            WorkOrderStatus.WaitingApproval,
            WorkOrderStatus.Diagnosing);
    }

    [Fact]
    public void CompleteEstimateService_ShouldTransitionFromInProgressToCompleted()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimate = Assert.Single(workOrder.Estimates);
        var service = Assert.Single(estimate.ServiceLines);
        workOrder.StartEstimateService(estimate.Id, service.Id);

        AssertStatusChange(
            workOrder,
            () => workOrder.CompleteEstimateService(estimate.Id, service.Id),
            WorkOrderStatus.InProgress,
            WorkOrderStatus.Completed);
    }

    [Fact]
    public void Deliver_ShouldTransitionFromCompletedToDelivered()
    {
        var workOrder = new WorkOrderBuilder().BuildCompleted();

        AssertStatusChange(workOrder, workOrder.Deliver, WorkOrderStatus.Completed, WorkOrderStatus.Delivered);
    }

    [Theory]
    [InlineData("Received")]
    [InlineData("Diagnosing")]
    [InlineData("WaitingApproval")]
    [InlineData("InProgress")]
    public void Cancel_ShouldTransitionActiveStatusToCancelled(string sourceStatus)
    {
        var workOrder = CreateInStatus(sourceStatus);
        var expectedPrevious = sourceStatus == "Received"
            ? (WorkOrderStatus)1
            : Enum.Parse<WorkOrderStatus>(sourceStatus);

        AssertStatusChange(workOrder, () => _ = workOrder.Cancel(), expectedPrevious, WorkOrderStatus.Cancelled);
    }

    [Fact]
    public void LifecycleOperations_ShouldRejectSkippedAndBackwardTransitions()
    {
        var received = new WorkOrderBuilder().BuildCreated();
        Assert.Throws<BusinessRuleViolationException>(received.Deliver);
        Assert.Throws<BusinessRuleViolationException>(received.Complete);

        var waitingApproval = new WorkOrderBuilder().BuildWithPendingEstimate();
        Assert.Throws<BusinessRuleViolationException>(waitingApproval.Deliver);
        Assert.Throws<BusinessRuleViolationException>(waitingApproval.StartDiagnosis);

        var inProgress = new WorkOrderBuilder().BuildWithApprovedEstimate();
        Assert.Throws<BusinessRuleViolationException>(inProgress.StartDiagnosis);
        Assert.Throws<BusinessRuleViolationException>(inProgress.Deliver);

        var completed = new WorkOrderBuilder().BuildCompleted();
        Assert.Throws<BusinessRuleViolationException>(completed.StartDiagnosis);
        Assert.Throws<BusinessRuleViolationException>(() => completed.Cancel());

        var delivered = new WorkOrderBuilder().BuildDelivered();
        Assert.Throws<BusinessRuleViolationException>(delivered.StartDiagnosis);
        Assert.Throws<BusinessRuleViolationException>(() => delivered.Cancel());
    }

    private static WorkOrder CreateInStatus(string status)
    {
        var builder = new WorkOrderBuilder();
        return status switch
        {
            "Received" => builder.BuildCreated(),
            "Diagnosing" => CreateDiagnosing(builder),
            "WaitingApproval" => builder.BuildWithPendingEstimate(),
            "InProgress" => builder.BuildWithApprovedEstimate(),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private static WorkOrder CreateDiagnosing(WorkOrderBuilder builder)
    {
        var workOrder = builder.BuildCreated();
        workOrder.StartDiagnosis();
        return workOrder;
    }

    private static WorkOrderStatusChanged AssertStatusChange(
        WorkOrder workOrder,
        Action transition,
        WorkOrderStatus expectedPrevious,
        WorkOrderStatus expectedNew)
    {
        var previousUpdatedAt = workOrder.UpdatedAt;
        workOrder.ClearDomainEvents();
        WaitUntilUtcClockAdvancesPast(previousUpdatedAt);

        transition();

        Assert.Equal(expectedNew, workOrder.Status);
        var changed = Assert.Single(workOrder.DomainEvents.OfType<WorkOrderStatusChanged>());
        AssertStatusChanged(changed, expectedPrevious, expectedNew);
        Assert.True(
            changed.UpdatedAt > previousUpdatedAt,
            $"Expected transition timestamp {changed.UpdatedAt:O} to advance past {previousUpdatedAt:O}.");
        Assert.True(workOrder.UpdatedAt >= changed.UpdatedAt);
        return changed;
    }

    private static void WaitUntilUtcClockAdvancesPast(DateTime timestamp)
    {
        var stopwatch = Stopwatch.StartNew();
        while (DateTime.UtcNow <= timestamp && stopwatch.Elapsed < TimeSpan.FromSeconds(1))
        {
            Thread.SpinWait(100);
        }

        Assert.True(DateTime.UtcNow > timestamp, "UTC clock did not advance within one second.");
    }

    private static void AssertStatusChanged(
        WorkOrderStatusChanged changed,
        WorkOrderStatus expectedPrevious,
        WorkOrderStatus expectedNew)
    {
        Assert.Equal(expectedPrevious, changed.PreviousStatus);
        Assert.Equal(expectedNew, changed.NewStatus);
    }
}
