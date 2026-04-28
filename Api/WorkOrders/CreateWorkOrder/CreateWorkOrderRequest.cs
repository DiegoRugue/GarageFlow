namespace GarageFlow.Api.WorkOrders.CreateWorkOrder;

public sealed record CreateWorkOrderRequest(Guid CustomerId, Guid VehicleId);
