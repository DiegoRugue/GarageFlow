using Mediator;

namespace GarageFlow.Application.WorkOrders.StartWork;

public sealed record StartWorkCommand(Guid WorkOrderId) : IRequest<Unit>;
