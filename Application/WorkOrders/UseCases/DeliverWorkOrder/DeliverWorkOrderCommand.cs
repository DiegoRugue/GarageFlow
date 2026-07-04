using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.DeliverWorkOrder;

public sealed record DeliverWorkOrderCommand(Guid WorkOrderId) : ICommand;
