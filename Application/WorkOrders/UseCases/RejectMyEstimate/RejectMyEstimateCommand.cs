using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.RejectMyEstimate;

public sealed record RejectMyEstimateCommand(Guid UserId, Guid WorkOrderId, Guid EstimateId) : IRequest<Unit>;
