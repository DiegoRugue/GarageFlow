using Mediator;

namespace GarageFlow.Application.WorkOrders.CancelWorkOrder;

public sealed record CancelWorkOrderCommand(Guid WorkOrderId) : IRequest<Unit>;
