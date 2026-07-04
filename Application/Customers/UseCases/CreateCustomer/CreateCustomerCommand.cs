using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Customers.UseCases.CreateCustomer;

public sealed record CreateCustomerCommand(
    string TaxDocument,
    string FullName,
    string Email,
    string PhoneNumber) : ICommand<CreateCustomerResult>;
