using Mediator;

namespace GarageFlow.Application.Customers.CreateCustomer;

public sealed record CreateCustomerCommand(
    string TaxDocument,
    string FullName,
    string Email,
    string PhoneNumber) : IRequest<CreateCustomerResult>;
