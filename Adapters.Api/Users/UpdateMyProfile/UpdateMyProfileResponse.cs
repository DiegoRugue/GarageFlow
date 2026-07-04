using GarageFlow.Domain.Users.Enums;

namespace GarageFlow.Adapters.Api.Users.UpdateMyProfile;

public sealed record UpdateMyProfileResponse(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime UpdatedAt);
