using GarageFlow.Domain.Users.Enums;

namespace GarageFlow.Application.Users.UseCases.ListUsers;

public sealed record UserListItemDto(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime UpdatedAt);
