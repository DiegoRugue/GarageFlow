using Mediator;

namespace GarageFlow.Application.Customers.UseCases.DeleteCustomer;

public sealed record DeleteCustomerCommand(Guid Id) : IRequest<Unit>;
