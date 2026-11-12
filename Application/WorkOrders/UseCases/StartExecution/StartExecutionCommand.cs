using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.StartExecution;

public sealed record StartExecutionCommand(Guid WorkOrderId) : ICommand;
