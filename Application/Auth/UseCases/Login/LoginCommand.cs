using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Auth.UseCases.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResult>;
