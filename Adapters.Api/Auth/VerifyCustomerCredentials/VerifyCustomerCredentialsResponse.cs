namespace GarageFlow.Adapters.Api.Auth.VerifyCustomerCredentials;

public sealed record VerifyCustomerCredentialsResponse(
    Guid UserId,
    Guid CustomerId,
    string Role,
    bool MustChangePassword);
