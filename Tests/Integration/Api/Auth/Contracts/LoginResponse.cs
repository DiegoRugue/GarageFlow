namespace GarageFlow.Tests.Integration.Api.Auth.Contracts;

public sealed record LoginResponse(
    string Token,
    bool MustChangePassword);
