namespace GarageFlow.Application.WorkOrders.Common;

public static class ExecutionSchedulingContract
{
    public const string SchemaVersion = "1.0";
    public const string EventKey = "execution.schedule.v1";
    public const string AcceptedKey = "execution.scheduled.v1";
    public const string RejectedKey = "execution.schedule-rejected.v1";
    public const string StartKey = "execution.start.v1";
    public const string CompleteKey = "execution.complete.v1";
    public const string StartedKey = "execution.started.v1";
    public const string CompletedKey = "execution.completed.v1";
    public const string AlreadyScheduledReason = "work_order_already_scheduled";
}
