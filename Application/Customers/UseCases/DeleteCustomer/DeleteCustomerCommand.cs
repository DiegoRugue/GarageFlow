using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Customers.UseCases.DeleteCustomer;

public sealed record DeleteCustomerCommand(Guid Id) : ICommand;
