namespace GarageFlow.Application.WorkOrders.CreateWorkOrder;

public sealed record CreateWorkOrderResult(Guid Id, Guid CustomerId, Guid VehicleId, string Status, DateTime CreatedAt);
