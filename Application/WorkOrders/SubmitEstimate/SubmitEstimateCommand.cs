using Mediator;

namespace GarageFlow.Application.WorkOrders.SubmitEstimate;

public sealed record SubmitEstimateCommand(Guid WorkOrderId, Guid EstimateId) : IRequest<Unit>;
