using Mediator;

namespace GarageFlow.Application.Customers.ActivateCustomerPortalUser;

public sealed record ActivateCustomerPortalUserCommand(
    Guid CustomerId,
    DateOnly BirthDate) : IRequest<ActivateCustomerPortalUserResult>;
