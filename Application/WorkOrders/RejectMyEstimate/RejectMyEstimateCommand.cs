using Mediator;

namespace GarageFlow.Application.WorkOrders.RejectMyEstimate;

public sealed record RejectMyEstimateCommand(Guid UserId, Guid WorkOrderId, Guid EstimateId) : IRequest<Unit>;
