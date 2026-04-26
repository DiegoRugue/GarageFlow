using Mediator;

namespace GarageFlow.Application.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResult>;
