using Mediator;

namespace GarageFlow.Application.Customers.UseCases.GetCustomerById;

public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<CustomerDto>;
