using Mediator;

namespace GarageFlow.Application.Customers.UpdateCustomer;

public sealed record UpdateCustomerCommand(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber) : IRequest<UpdateCustomerResult>;
