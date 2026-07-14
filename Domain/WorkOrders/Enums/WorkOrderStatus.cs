namespace GarageFlow.Domain.WorkOrders.Enums;

public enum WorkOrderStatus
{
    Received = 1,
    Diagnosing = 2,
    WaitingApproval = 3,
    InProgress = 5,
    Completed = 6,
    Delivered = 7,
    Cancelled = 8
}
