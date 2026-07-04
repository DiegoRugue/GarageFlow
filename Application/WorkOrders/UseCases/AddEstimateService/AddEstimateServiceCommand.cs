using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.AddEstimateService;

public sealed record AddEstimateServiceCommand(Guid WorkOrderId, Guid EstimateId, Guid ServiceId) : IRequest<AddEstimateServiceResult>;
