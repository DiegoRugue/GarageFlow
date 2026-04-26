using GarageFlow.Application.Customers.GetCustomerById;

namespace GarageFlow.Application.Customers.ListCustomers;

public sealed record ListCustomersResult(
    IReadOnlyList<CustomerDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
