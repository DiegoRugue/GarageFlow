namespace GarageFlow.Api.WorkOrders.CreateWorkOrder;

public sealed record CreateWorkOrderResponse(
    Guid Id,
    Guid CustomerId,
    Guid VehicleId,
    string Status,
    DateTime CreatedAt);
