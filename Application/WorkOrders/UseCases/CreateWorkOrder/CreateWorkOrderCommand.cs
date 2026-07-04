using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrder;

public sealed record CreateWorkOrderCommand(Guid CustomerId, Guid VehicleId) : IRequest<CreateWorkOrderResult>;
