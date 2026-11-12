using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.CompleteExecution;

public sealed record CompleteExecutionCommand(Guid WorkOrderId) : ICommand;
