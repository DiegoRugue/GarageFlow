using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.DeliverWorkOrder;

public sealed record DeliverWorkOrderCommand(Guid WorkOrderId) : IRequest<Unit>;
