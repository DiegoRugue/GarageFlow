using Mediator;

namespace GarageFlow.Application.WorkOrders.StartEstimateService;

public sealed record StartEstimateServiceCommand(
    Guid WorkOrderId,
    Guid EstimateId,
    Guid LineId) : IRequest<Unit>;
