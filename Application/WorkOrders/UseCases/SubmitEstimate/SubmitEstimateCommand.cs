using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.SubmitEstimate;

public sealed record SubmitEstimateCommand(Guid WorkOrderId, Guid EstimateId) : IRequest<Unit>;
