using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.Users.ValueObjects;

namespace GarageFlow.Domain.Users.Events;

public sealed record UserPasswordChanged(
    UserId UserId,
    bool MustChangePassword,
    DateTime UpdatedAt) : DomainEvent;
