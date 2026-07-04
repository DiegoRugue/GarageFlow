namespace GarageFlow.Application.Auth.UseCases.Login;

public sealed record LoginResult(
    string Token,
    bool MustChangePassword);
