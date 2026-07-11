namespace GarageFlow.Application.Users.UseCases.ListUsers;

public sealed record UserListItemDto(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    int Role,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime UpdatedAt);
