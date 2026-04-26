using Mediator;

namespace GarageFlow.Application.Users.DeleteUser;

public sealed record DeleteUserCommand(Guid Id) : IRequest<Unit>;
