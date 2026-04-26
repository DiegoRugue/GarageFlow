using Mediator;

namespace GarageFlow.Application.Customers.ListCustomers;

public sealed record ListCustomersQuery(int Page = 1, int PageSize = 20) : IRequest<ListCustomersResult>;
