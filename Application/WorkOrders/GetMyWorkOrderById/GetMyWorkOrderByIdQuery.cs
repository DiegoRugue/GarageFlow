using Mediator;

namespace GarageFlow.Application.WorkOrders.GetMyWorkOrderById;

public sealed record GetMyWorkOrderByIdQuery(Guid UserId, Guid WorkOrderId) : IRequest<CustomerWorkOrderDetailsDto>;
