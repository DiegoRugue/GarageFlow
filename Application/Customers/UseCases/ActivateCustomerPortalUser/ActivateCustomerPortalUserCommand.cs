using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Customers.UseCases.ActivateCustomerPortalUser;

public sealed record ActivateCustomerPortalUserCommand(
    Guid CustomerId,
    DateOnly BirthDate) : ICommand<ActivateCustomerPortalUserResult>;
