namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderStatus;

public sealed record GetWorkOrderStatusResult(
    Guid Id,
    string Status,
    DateTime UpdatedAt);
