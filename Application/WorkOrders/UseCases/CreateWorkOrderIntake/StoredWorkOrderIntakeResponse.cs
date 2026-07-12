namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;

public sealed record StoredWorkOrderIntakeResponse(
    Guid WorkOrderId,
    Guid CustomerId,
    Guid VehicleId,
    Guid EstimateId,
    IReadOnlyList<Guid> ServiceIds,
    IReadOnlyList<Guid> InventoryItemIds,
    string Status,
    DateTime CreatedAt);
