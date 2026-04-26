using GarageFlow.Domain.Users.Enums;

namespace GarageFlow.Application.Users.ListUsers;

public sealed record UserListItem(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime UpdatedAt);
