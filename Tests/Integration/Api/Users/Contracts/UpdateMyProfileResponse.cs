using GarageFlow.Domain.Users.Enums;

namespace GarageFlow.Tests.Integration.Api.Users.Contracts;

public sealed record UpdateMyProfileResponse(
    Guid Id,
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime UpdatedAt);
