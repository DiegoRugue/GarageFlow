using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.ListMyWorkOrders;

public sealed record ListMyWorkOrdersQuery(Guid UserId, int Page = 1, int PageSize = 20) : IRequest<ListMyWorkOrdersResult>;
