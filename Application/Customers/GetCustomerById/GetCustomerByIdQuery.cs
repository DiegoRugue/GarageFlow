using Mediator;

namespace GarageFlow.Application.Customers.GetCustomerById;

public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<CustomerDto?>;
