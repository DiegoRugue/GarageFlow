namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Idempotency;

public sealed class IntakeRequestReceipt
{
    public Guid RequestId { get; private set; }
    public string PayloadHash { get; private set; } = string.Empty;
    public Guid? WorkOrderId { get; private set; }
    public string? ResponseJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private IntakeRequestReceipt()
    {
    }

    public static IntakeRequestReceipt Create(Guid requestId, string payloadHash, DateTime createdAt) => new()
    {
        RequestId = requestId,
        PayloadHash = payloadHash,
        CreatedAt = createdAt
    };

    public void Complete(Guid workOrderId, string responseJson, DateTime completedAt)
    {
        WorkOrderId = workOrderId;
        ResponseJson = responseJson;
        CompletedAt = completedAt;
    }
}
