namespace GarageFlow.Application.WorkOrders.ReadModels;

public sealed record WorkOrderStatusReadModel(
    Guid Id,
    string Status,
    DateTime UpdatedAt);
