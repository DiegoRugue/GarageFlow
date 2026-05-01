using Mediator;

namespace GarageFlow.Application.WorkOrders.CreateWorkOrder;

public sealed record CreateWorkOrderCommand(Guid CustomerId, Guid VehicleId) : IRequest<CreateWorkOrderResult>;
