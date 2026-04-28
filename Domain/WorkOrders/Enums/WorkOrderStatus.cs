namespace GarageFlow.Domain.WorkOrders.Enums;

public enum WorkOrderStatus
{
    Created = 1,
    Diagnosing = 2,
    WaitingApproval = 3,
    Approved = 4,
    InProgress = 5,
    Completed = 6,
    Delivered = 7,
    Cancelled = 8
}
