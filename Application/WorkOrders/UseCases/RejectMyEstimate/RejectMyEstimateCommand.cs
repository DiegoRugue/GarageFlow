using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.RejectMyEstimate;

public sealed record RejectMyEstimateCommand(Guid UserId, Guid WorkOrderId, Guid EstimateId) : ICommand;
