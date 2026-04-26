using GarageFlow.Api.Customers.GetCustomerById;

namespace GarageFlow.Api.Customers.ListCustomers;

public sealed record ListCustomersResponse(
    IReadOnlyList<CustomerResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
