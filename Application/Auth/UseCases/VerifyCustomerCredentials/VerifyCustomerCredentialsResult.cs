namespace GarageFlow.Application.Auth.UseCases.VerifyCustomerCredentials;

public sealed record VerifyCustomerCredentialsResult(
    Guid UserId,
    Guid CustomerId,
    string Role,
    bool MustChangePassword);
