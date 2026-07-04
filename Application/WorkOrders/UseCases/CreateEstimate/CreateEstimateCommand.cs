using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.CreateEstimate;

public sealed record CreateEstimateCommand(Guid WorkOrderId) : IRequest<CreateEstimateResult>;
