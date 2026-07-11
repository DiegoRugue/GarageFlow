using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Customers.UseCases.UpdateCustomer;

public sealed record UpdateCustomerCommand(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber) : ICommand<UpdateCustomerResult>;
