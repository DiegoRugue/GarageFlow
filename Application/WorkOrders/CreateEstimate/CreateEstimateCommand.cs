using Mediator;

namespace GarageFlow.Application.WorkOrders.CreateEstimate;

public sealed record CreateEstimateCommand(Guid WorkOrderId) : IRequest<CreateEstimateResult>;
