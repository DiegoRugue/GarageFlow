namespace GarageFlow.Adapters.Api.Auth.Login;

public sealed record LoginRequest(
    string Email,
    string Password);
