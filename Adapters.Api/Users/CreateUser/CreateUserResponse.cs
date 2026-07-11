namespace GarageFlow.Adapters.Api.Users.CreateUser;

public sealed record CreateUserResponse(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    int Role,
    bool MustChangePassword,
    DateTime CreatedAt);
