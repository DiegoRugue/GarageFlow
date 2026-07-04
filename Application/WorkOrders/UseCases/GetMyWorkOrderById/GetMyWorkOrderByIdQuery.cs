using GarageFlow.Application.WorkOrders.Common;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetMyWorkOrderById;

public sealed record GetMyWorkOrderByIdQuery(Guid UserId, Guid WorkOrderId) : IRequest<CustomerWorkOrderDetailsDto>;
