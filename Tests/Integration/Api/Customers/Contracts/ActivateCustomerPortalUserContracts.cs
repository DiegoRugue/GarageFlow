namespace GarageFlow.Tests.Integration.Api.Customers.Contracts;

public sealed record ActivateCustomerPortalUserRequest(DateOnly BirthDate);

public sealed record ActivateCustomerPortalUserResponse(
    Guid Id,
    Guid CustomerId,
    string FullName,
    string Email,
    DateOnly BirthDate,
    string Role,
    bool MustChangePassword,
    DateTime CreatedAt);
