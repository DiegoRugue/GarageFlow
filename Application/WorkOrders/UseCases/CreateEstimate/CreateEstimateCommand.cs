using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.CreateEstimate;

public sealed record CreateEstimateCommand(Guid WorkOrderId) : ICommand<CreateEstimateResult>;
