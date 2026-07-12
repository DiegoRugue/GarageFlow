namespace GarageFlow.Adapters.Api.WorkOrders.GetWorkOrderStatus;

public sealed record GetWorkOrderStatusResponse(
    Guid Id,
    string Status,
    DateTime UpdatedAt);
