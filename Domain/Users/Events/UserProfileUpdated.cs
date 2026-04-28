using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Domain.Users.ValueObjects;

namespace GarageFlow.Domain.Users.Events;

public sealed record UserProfileUpdated(
    UserId UserId,
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role,
    bool MustChangePassword,
    DateTime UpdatedAt) : DomainEvent;
