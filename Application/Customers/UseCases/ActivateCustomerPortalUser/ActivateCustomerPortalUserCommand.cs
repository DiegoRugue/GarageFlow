using Mediator;

namespace GarageFlow.Application.Customers.UseCases.ActivateCustomerPortalUser;

public sealed record ActivateCustomerPortalUserCommand(
    Guid CustomerId,
    DateOnly BirthDate) : IRequest<ActivateCustomerPortalUserResult>;
