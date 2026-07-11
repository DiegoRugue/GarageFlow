namespace GarageFlow.Tests.Integration.Api.Users.Contracts;

public sealed record UpdateMyProfileResponse(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    int Role,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime UpdatedAt);
