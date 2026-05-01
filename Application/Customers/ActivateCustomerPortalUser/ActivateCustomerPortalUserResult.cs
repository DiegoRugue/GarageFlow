namespace GarageFlow.Application.Customers.ActivateCustomerPortalUser;

public sealed record ActivateCustomerPortalUserResult(
    Guid Id,
    Guid CustomerId,
    string FullName,
    string Email,
    DateOnly BirthDate,
    string Role,
    bool MustChangePassword,
    DateTime CreatedAt);
