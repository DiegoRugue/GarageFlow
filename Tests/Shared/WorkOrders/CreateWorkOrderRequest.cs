namespace GarageFlow.Tests.Shared.WorkOrders;

public sealed record CreateWorkOrderRequest(Guid CustomerId, Guid VehicleId);
