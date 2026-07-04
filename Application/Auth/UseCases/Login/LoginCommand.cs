using Mediator;

namespace GarageFlow.Application.Auth.UseCases.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResult>;
