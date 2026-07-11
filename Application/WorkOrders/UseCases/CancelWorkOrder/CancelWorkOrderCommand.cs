using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.CancelWorkOrder;

public sealed record CancelWorkOrderCommand(Guid WorkOrderId) : ICommand;
