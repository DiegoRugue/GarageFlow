namespace GarageFlow.Application.WorkOrders.ReadModels;

public sealed record WorkOrderDetailsReadModel(
    Guid Id,
    Guid CustomerId,
    Guid VehicleId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkOrderEstimateReadModel> Estimates);
