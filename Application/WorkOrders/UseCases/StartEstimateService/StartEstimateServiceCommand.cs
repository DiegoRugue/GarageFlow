using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.StartEstimateService;

public sealed record StartEstimateServiceCommand(
    Guid WorkOrderId,
    Guid EstimateId,
    Guid LineId) : IRequest<Unit>;
