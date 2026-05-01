using Mediator;

namespace GarageFlow.Application.WorkOrders.AddEstimateService;

public sealed record AddEstimateServiceCommand(Guid WorkOrderId, Guid EstimateId, Guid ServiceId) : IRequest<AddEstimateServiceResult>;
