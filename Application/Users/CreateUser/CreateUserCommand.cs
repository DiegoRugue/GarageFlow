using GarageFlow.Domain.Users.Enums;
using Mediator;

namespace GarageFlow.Application.Users.CreateUser;

public sealed record CreateUserCommand(
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role) : IRequest<CreateUserResult>;
