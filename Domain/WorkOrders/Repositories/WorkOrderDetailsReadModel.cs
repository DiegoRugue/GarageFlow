namespace GarageFlow.Domain.WorkOrders.Repositories;

public sealed record WorkOrderDetailsReadModel(
    Guid Id,
    Guid CustomerId,
    Guid VehicleId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkOrderEstimateReadModel> Estimates);
