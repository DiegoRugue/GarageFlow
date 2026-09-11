namespace GarageFlow.Adapters.Api.Auth.VerifyCustomerCredentials;

public sealed record VerifyCustomerCredentialsRequest(string Cpf, string Password);
