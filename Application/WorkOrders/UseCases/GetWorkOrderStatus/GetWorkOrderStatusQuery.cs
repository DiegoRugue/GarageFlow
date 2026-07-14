using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderStatus;

public sealed record GetWorkOrderStatusQuery(Guid Id) : IRequest<GetWorkOrderStatusResult>;
