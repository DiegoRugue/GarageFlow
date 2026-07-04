using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.CancelWorkOrder;

public sealed record CancelWorkOrderCommand(Guid WorkOrderId) : IRequest<Unit>;
