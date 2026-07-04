namespace GarageFlow.Application.Users.UseCases.CreateUser;

public sealed record CreateUserResult(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    int Role,
    bool MustChangePassword,
    DateTime CreatedAt);
