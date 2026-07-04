using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.CompleteEstimateService;

public sealed record CompleteEstimateServiceCommand(
    Guid WorkOrderId,
    Guid EstimateId,
    Guid LineId) : IRequest<Unit>;
