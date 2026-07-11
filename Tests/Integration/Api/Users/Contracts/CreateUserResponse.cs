namespace GarageFlow.Tests.Integration.Api.Users.Contracts;

public sealed record CreateUserResponse(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    int Role,
    bool MustChangePassword,
    DateTime CreatedAt);
