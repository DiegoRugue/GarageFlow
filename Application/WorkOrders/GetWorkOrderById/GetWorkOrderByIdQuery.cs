using Mediator;

namespace GarageFlow.Application.WorkOrders.GetWorkOrderById;

public sealed record GetWorkOrderByIdQuery(Guid Id) : IRequest<WorkOrderDetailsDto>;
