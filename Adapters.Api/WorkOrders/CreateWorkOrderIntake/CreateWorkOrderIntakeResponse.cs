namespace GarageFlow.Adapters.Api.WorkOrders.CreateWorkOrderIntake;

public sealed record CreateWorkOrderIntakeResponse(
    Guid WorkOrderId,
    Guid CustomerId,
    Guid VehicleId,
    Guid EstimateId,
    IReadOnlyList<Guid> ServiceIds,
    IReadOnlyList<Guid> InventoryItemIds,
    string Status,
    DateTime CreatedAt);
