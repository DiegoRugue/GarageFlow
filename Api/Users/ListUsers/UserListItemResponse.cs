using GarageFlow.Domain.Users.Enums;

namespace GarageFlow.Api.Users.ListUsers;

public sealed record UserListItemResponse(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime UpdatedAt);
