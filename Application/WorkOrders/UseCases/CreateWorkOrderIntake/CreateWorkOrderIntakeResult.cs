namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;

public sealed record CreateWorkOrderIntakeResult(
    bool IsReplay,
    Guid WorkOrderId,
    Guid CustomerId,
    Guid VehicleId,
    Guid EstimateId,
    IReadOnlyList<Guid> ServiceIds,
    IReadOnlyList<Guid> InventoryItemIds,
    string Status,
    DateTime CreatedAt);
