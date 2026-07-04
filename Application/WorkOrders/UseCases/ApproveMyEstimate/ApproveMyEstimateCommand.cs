using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.ApproveMyEstimate;

public sealed record ApproveMyEstimateCommand(Guid UserId, Guid WorkOrderId, Guid EstimateId) : IRequest<Unit>;
