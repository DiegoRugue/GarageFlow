using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.StartWork;

public sealed record StartWorkCommand(Guid WorkOrderId) : ICommand;
