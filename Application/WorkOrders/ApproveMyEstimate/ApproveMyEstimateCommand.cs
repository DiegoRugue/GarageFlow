using Mediator;

namespace GarageFlow.Application.WorkOrders.ApproveMyEstimate;

public sealed record ApproveMyEstimateCommand(Guid UserId, Guid WorkOrderId, Guid EstimateId) : IRequest<Unit>;
