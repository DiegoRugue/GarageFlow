namespace GarageFlow.Tests.E2E.Support.Contracts.Auth;

public sealed record LoginResponse(
    string Token,
    bool MustChangePassword);
