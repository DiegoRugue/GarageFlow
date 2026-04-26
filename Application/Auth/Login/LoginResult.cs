namespace GarageFlow.Application.Auth.Login;

public sealed record LoginResult(
    string Token,
    bool MustChangePassword);
