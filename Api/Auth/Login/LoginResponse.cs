namespace GarageFlow.Api.Auth.Login;

public sealed record LoginResponse(
    string Token,
    bool MustChangePassword);
