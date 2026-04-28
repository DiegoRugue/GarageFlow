using Mediator;

namespace GarageFlow.Application.WorkOrders.DeliverWorkOrder;

public sealed record DeliverWorkOrderCommand(Guid WorkOrderId) : IRequest<Unit>;
