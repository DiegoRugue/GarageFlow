using GarageFlow.Domain.Users.Enums;

namespace GarageFlow.Adapters.Api.Users.CreateUser;

public sealed record CreateUserResponse(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role,
    bool MustChangePassword,
    DateTime CreatedAt);
