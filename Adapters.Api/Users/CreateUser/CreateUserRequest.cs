using GarageFlow.Domain.Users.Enums;

namespace GarageFlow.Adapters.Api.Users.CreateUser;

public sealed record CreateUserRequest(
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role);
