namespace GarageFlow.Adapters.Api.Users.ListUsers;

public sealed record UserListItemResponse(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    int Role,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime UpdatedAt);
