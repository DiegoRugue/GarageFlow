using GarageFlow.BuildingBlocks.Domain.Events;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Domain.Users.ValueObjects;

namespace GarageFlow.Domain.Users.Events;

public sealed record UserCreated(
    UserId UserId,
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role,
    CustomerId? CustomerId,
    bool MustChangePassword,
    DateTime CreatedAt) : DomainEvent;
