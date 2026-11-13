namespace GarageFlow.Domain.WorkOrders.Enums;

public enum WorkOrderStatus
{
    Received = 1,
    Diagnosing = 2,
    WaitingApproval = 3,
    InProgress = 5,
    Completed = 6,
    Delivered = 7,
    Cancelled = 8,
    WaitingExecutionSchedule = 9,
    ExecutionScheduled = 10,
    ExecutionScheduleRejected = 11,
    ExecutionStartRequested = 12,
    ExecutionCompletionRequested = 13
}
