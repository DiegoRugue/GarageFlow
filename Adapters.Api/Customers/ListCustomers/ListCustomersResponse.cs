using GarageFlow.Adapters.Api.Customers.GetCustomerById;

namespace GarageFlow.Adapters.Api.Customers.ListCustomers;

public sealed record ListCustomersResponse(
    IReadOnlyList<CustomerResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
