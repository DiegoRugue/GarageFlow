namespace GarageFlow.Adapters.Api.Customers.ActivateCustomerPortalUser;

public sealed record ActivateCustomerPortalUserResponse(
    Guid Id,
    Guid CustomerId,
    string FullName,
    string Email,
    DateOnly BirthDate,
    string Role,
    bool MustChangePassword,
    DateTime CreatedAt);
