namespace GarageFlow.Adapters.Api.WorkOrders.CreateWorkOrder;

public sealed record CreateWorkOrderRequest(Guid CustomerId, Guid VehicleId);
