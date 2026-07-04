using Mediator;

namespace GarageFlow.Application.Users.UseCases.CreateUser;

public sealed record CreateUserCommand(
    string FullName,
    string Email,
    DateOnly BirthDate,
    int Role) : IRequest<CreateUserResult>;
