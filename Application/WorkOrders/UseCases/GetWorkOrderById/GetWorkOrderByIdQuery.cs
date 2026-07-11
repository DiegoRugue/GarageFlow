using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderById;

public sealed record GetWorkOrderByIdQuery(Guid Id) : IRequest<WorkOrderDetailsDto>;
