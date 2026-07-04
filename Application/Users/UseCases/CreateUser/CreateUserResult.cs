using GarageFlow.Domain.Users.Enums;

namespace GarageFlow.Application.Users.UseCases.CreateUser;

public sealed record CreateUserResult(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role,
    bool MustChangePassword,
    DateTime CreatedAt);
