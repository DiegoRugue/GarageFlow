namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrder;

public sealed record CreateWorkOrderResult(Guid Id, Guid CustomerId, Guid VehicleId, string Status, DateTime CreatedAt);
