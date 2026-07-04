using GarageFlow.Domain.Users.Enums;

namespace GarageFlow.Application.Users.UseCases.UpdateMyProfile;

public sealed record UpdateMyProfileResult(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime UpdatedAt);
