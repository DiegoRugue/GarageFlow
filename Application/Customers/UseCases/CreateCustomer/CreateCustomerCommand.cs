using Mediator;

namespace GarageFlow.Application.Customers.UseCases.CreateCustomer;

public sealed record CreateCustomerCommand(
    string TaxDocument,
    string FullName,
    string Email,
    string PhoneNumber) : IRequest<CreateCustomerResult>;
