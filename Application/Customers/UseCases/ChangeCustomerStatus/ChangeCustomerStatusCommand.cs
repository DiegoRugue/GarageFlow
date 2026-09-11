using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Customers.UseCases.ChangeCustomerStatus;

public sealed record ChangeCustomerStatusCommand(
    Guid CustomerId,
    string Status) : ICommand<ChangeCustomerStatusResult>;
