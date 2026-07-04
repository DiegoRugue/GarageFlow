using Mediator;

namespace GarageFlow.Application.Users.UseCases.DeleteUser;

public sealed record DeleteUserCommand(Guid Id) : IRequest<Unit>;
