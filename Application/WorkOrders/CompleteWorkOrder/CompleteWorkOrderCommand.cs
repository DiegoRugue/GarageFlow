using Mediator;

namespace GarageFlow.Application.WorkOrders.CompleteWorkOrder;

public sealed record CompleteWorkOrderCommand(Guid WorkOrderId) : IRequest<Unit>;
