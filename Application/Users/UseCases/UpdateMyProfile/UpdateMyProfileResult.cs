namespace GarageFlow.Application.Users.UseCases.UpdateMyProfile;

public sealed record UpdateMyProfileResult(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    int Role,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime UpdatedAt);
