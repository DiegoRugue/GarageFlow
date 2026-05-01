using Mediator;

namespace GarageFlow.Application.WorkOrders.ListWorkOrders;

public sealed record ListWorkOrdersQuery(int Page = 1, int PageSize = 20, Guid? CustomerId = null) : IRequest<ListWorkOrdersResult>;
