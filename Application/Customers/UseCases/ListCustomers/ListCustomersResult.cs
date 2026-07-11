using GarageFlow.Application.Customers.UseCases.GetCustomerById;

namespace GarageFlow.Application.Customers.UseCases.ListCustomers;

public sealed record ListCustomersResult(
    IReadOnlyList<CustomerDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
