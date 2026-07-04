using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.StartWork;

public sealed record StartWorkCommand(Guid WorkOrderId) : IRequest<Unit>;
