using Mediator;

namespace GarageFlow.Application.Customers.DeleteCustomer;

public sealed record DeleteCustomerCommand(Guid Id) : IRequest<Unit>;
